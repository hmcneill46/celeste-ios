using System.Buffers.Binary;
using System.Diagnostics;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using AssemblyDefinition = Mono.Cecil.AssemblyDefinition;
using MethodDefinition = Mono.Cecil.MethodDefinition;
using TypeDefinition = Mono.Cecil.TypeDefinition;
using TypeReference = Mono.Cecil.TypeReference;

namespace AppleEverestBuilder;

// HOST ONLY. Apple replaces managed method bodies after AOT. Inspect real IL
// before that operation, then bind metadata, initialized data, native code and
// the signed app instead of interpreting a stripped one-byte ret as evidence.
internal static partial class AotFactoryProductInspection
{
    internal sealed record Request(string LinkedAssembly, string StrippedAssembly, string PackagedAssembly,
        string LlvmObject, string MonoObject, string NativeImage, string PackagedNativeImage,
        string AotData, string PackagedAotData, string NativeMain, string AotProvenance);

    internal static void Write(string requestPath, string manifestPath, string profilesPath, string output)
    {
        Request request = JsonSerializer.Deserialize<Request>(File.ReadAllText(requestPath))
            ?? throw new InvalidDataException("AOT product request absent");
        object provenance = VerifyProvenance(request);
        var graph = SelectedFactoryTypeClosure.LoadAndValidate(manifestPath);
        using JsonDocument profiles = JsonDocument.Parse(File.ReadAllBytes(profilesPath));
        RuntimeClosureScanner.Verify(request.LinkedAssembly);
        var factories = CompiledFactoryInspection.Inspect(request.LinkedAssembly, profiles.RootElement, graph.Manifest.Factories);
        object metadata = CompareManaged(request.LinkedAssembly, request.StrippedAssembly);
        EqualFile(request.StrippedAssembly, request.PackagedAssembly, "packaged stripped assembly");
        EqualFile(request.AotData, request.PackagedAotData, "packaged AOT data");
        string Sibling(string path, string name) => Path.Combine(Path.GetDirectoryName(path)!, name);
        string djLinked = Sibling(request.LinkedAssembly, "DJMapHelper.dll");
        string djStripped = Sibling(request.StrippedAssembly, "DJMapHelper.dll");
        object companionMetadata = CompareManaged(djLinked, djStripped);
        EqualFile(djStripped, Sibling(request.PackagedAssembly, "DJMapHelper.dll"), "packaged DJMapHelper stripped assembly");
        EqualFile(Sibling(request.AotData, "DJMapHelper.aotdata"), Sibling(request.PackagedAotData, "DJMapHelper.aotdata.arm64"), "packaged DJMapHelper AOT data");
        using DefaultAssemblyResolver resolver = new();
        resolver.AddSearchDirectory(Path.GetDirectoryName(request.LinkedAssembly)!);
        using AssemblyDefinition linked = AssemblyDefinition.ReadAssembly(request.LinkedAssembly, new ReaderParameters { AssemblyResolver = resolver });
        using AssemblyDefinition dj = AssemblyDefinition.ReadAssembly(Path.Combine(Path.GetDirectoryName(request.LinkedAssembly)!, "DJMapHelper.dll"), new ReaderParameters { AssemblyResolver = resolver });
        string[] entries = factories.Select(factory => SelectedFactoryProfiles.EntryMethod(factory.Kind, factory.CustomId)).ToArray();
        TypeDefinition registry = linked.MainModule.GetType("Celeste.Mod.GeneratedAppleEverestGameplayRegistry");
        Dictionary<string, MethodDefinition> rootMethods = new(StringComparer.Ordinal);
        void Add(MethodDefinition method)
        {
            if (!method.HasBody || method.HasGenericParameters) throw new InvalidDataException("native root is not a concrete method: " + method.FullName);
            string symbol = Symbol(method);
            if (rootMethods.TryGetValue(symbol, out MethodDefinition? previous) && previous.FullName != method.FullName)
                throw new InvalidDataException("native root mangling collision: " + symbol);
            rootMethods[symbol] = method;
        }
        void Creator(MethodDefinition method)
        {
            Add(method);
            foreach (MethodReference called in method.Body.Instructions.Select(i => i.Operand).OfType<MethodReference>()
                         .Where(called => called.DeclaringType.Namespace == "Celeste.Mod" && called.Name is "Create" or "Offset" or "Target" or "Smooth" or "Attach"))
                Creator(called.Resolve());
        }
        foreach (MethodDefinition method in registry.Methods.Where(method => entries.Contains(method.Name)))
            Creator(method);
        foreach (string kind in new[] { "Entity", "Trigger", "Backdrop" })
            Add(registry.Methods.Single(method => method.Name == "Select" + kind));
        foreach (MethodDefinition method in Types(linked.MainModule.GetType("Celeste.Mod.AppleEverestSelectedProfileGuard"))
                     .Append(linked.MainModule.GetType("Celeste.Mod.AppleEverestFlagToggleComponent")).SelectMany(type => type.Methods).Where(method => method.HasBody)) Add(method);
        var methods = new[] { linked, dj }.SelectMany(assembly => assembly.MainModule.Types.SelectMany(Types))
            .SelectMany(type => type.Methods).ToLookup(method => method.FullName, StringComparer.Ordinal);
        foreach (var factory in factories)
        foreach (JsonElement closure in JsonSerializer.SerializeToElement(factory.TypeClosure).EnumerateArray())
        foreach (string category in new[] { "constructors", "lifecycle" })
        foreach (JsonElement method in closure.GetProperty(category).EnumerateArray())
            if (method.GetProperty("bodySha256").ValueKind != JsonValueKind.Null) Add(methods[method.GetProperty("method").GetString()!].Single());
        string[] roots = rootMethods.Keys.Order(StringComparer.Ordinal).ToArray();
        if (entries.Length != 73 || roots.Length < 296) throw new InvalidDataException("selected native root census differs");
        Dictionary<string, char> native = Symbols(request.NativeImage, roots.Concat(new[] { "_mono_aot_module_Celeste_info", "_mono_aot_module_DJMapHelper_info" }));
        foreach (var group in rootMethods.GroupBy(pair => pair.Value.Module.Assembly.Name.Name))
        {
            string obj = Path.Combine(Path.GetDirectoryName(request.LlvmObject)!, group.Key + ".dll.llvm.o");
            RequireNativeDefinitions(obj, native, group.Select(pair => pair.Key));
        }
        byte[] image = File.ReadAllBytes(request.NativeImage), packaged = File.ReadAllBytes(request.PackagedNativeImage);
        foreach (AssemblyDefinition assembly in new[] { linked, dj })
        {
            string owner = assembly.Name.Name, module = "_mono_aot_module_" + owner + "_info";
            if (!native.TryGetValue(module, out char moduleKind) || moduleKind is not ('D' or 'd') ||
                !Regex.IsMatch(File.ReadAllText(request.NativeMain), @"(?m)^\s*mono_aot_register_module\s*\(\s*" + Regex.Escape(module[1..]) + @"\s*\)\s*;\s*$"))
                throw new InvalidDataException(owner + " AOT module is not statically registered");
            string mvid = assembly.MainModule.Mvid.ToString("D").ToUpperInvariant();
            byte[] identity = Encoding.ASCII.GetBytes(mvid + "\0\0\0\0" + owner + "\0");
            if (Count(image, identity) != 1 || Count(packaged, identity) != 1 ||
                Count(File.ReadAllBytes(Sibling(request.LlvmObject, owner + ".dll.llvm.o")), Encoding.ASCII.GetBytes(mvid + "\0")) != 1 ||
                Count(File.ReadAllBytes(Sibling(request.AotData, owner + ".aotdata")), Encoding.ASCII.GetBytes(mvid + "\0")) != 1)
                throw new InvalidDataException("native AOT identity is not paired with the exact linked " + owner + " MVID");
        }
        var nativeSections = MachO(image); var packagedSections = MachO(packaged);
        if (JsonSerializer.Serialize(nativeSections) != JsonSerializer.Serialize(packagedSections))
            throw new InvalidDataException("packaged native code/data sections differ from the inspected native image");
        object report = new { schemaVersion = 1, authority = "ACTUAL_LINKED_IL_AOT_CODE_DEFINITIONS_EXACT_STRIPPING_METADATA_NATIVE_SECTION_BINDING",
            selectedFactories = factories.Length, acceptedOccurrences = factories.Sum(factory => factory.AcceptedOccurrences),
            linkedForbiddenCallScan = true, strippedExecutableBodiesUsedAsProof = false,
            linkedAssemblySha256 = Hashing.FileSha256(request.LinkedAssembly), packagedAssemblySha256 = Hashing.FileSha256(request.PackagedAssembly),
            llvmObjectSha256 = Hashing.FileSha256(request.LlvmObject), monoObjectSha256 = Hashing.FileSha256(request.MonoObject),
            nativeImageSha256 = Hashing.FileSha256(request.NativeImage), packagedNativeImageSha256 = Hashing.FileSha256(request.PackagedNativeImage),
            aotDataSha256 = Hashing.FileSha256(request.AotData), exactNativeRoots = roots, provenance, metadata, companionMetadata, nativeSections, factories,
            semanticPhysicalAcceptance = "SEPARATE_REQUIRED_GATE" };
        File.WriteAllText(output, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }) + "\n");
    }

    private static object VerifyProvenance(Request request)
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(request.AotProvenance));
        JsonElement receipt = document.RootElement, fields = receipt.GetProperty("fields");
        if (receipt.GetProperty("schemaVersion").GetInt32() != 1 ||
            receipt.GetProperty("phase").GetString() != "AFTER_NATIVE_LINK" ||
            receipt.GetProperty("boundary").GetString() != "AppleSDK._AOTCompile")
            throw new InvalidDataException("actual AOT compiler boundary receipt absent");
        if (!Regex.IsMatch(receipt.GetProperty("sourceCommit").GetString()!, "^[0-9a-f]{40}$") ||
            receipt.GetProperty("sourceTreeDirty").ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            throw new InvalidDataException("actual AOT source revision binding absent");
        void Check(string file, JsonElement hash, string label)
        { if (Hashing.FileSha256(file) != hash.GetString()) throw new InvalidDataException("AOT provenance content changed: " + label); }
        void Mapping(string key, string path)
        { if (Path.GetFullPath(fields.GetProperty(key).GetString()!) != Path.GetFullPath(path)) throw new InvalidDataException("actual AOT input/output mapping differs: " + key); }
        Mapping("LinkedAssembly", request.LinkedAssembly); Mapping("LLVMFile", request.LlvmObject);
        Mapping("ObjectFile", request.MonoObject); Mapping("AOTData", request.AotData);
        Check(request.LinkedAssembly, receipt.GetProperty("inputHashes").GetProperty("LinkedAssembly"), "linked IL");
        foreach (var pair in new[] { ("LLVMFile", request.LlvmObject), ("ObjectFile", request.MonoObject), ("AOTData", request.AotData) })
            Check(pair.Item2, receipt.GetProperty("outputHashes").GetProperty(pair.Item1), pair.Item1);
        JsonElement dj = receipt.GetProperty("companions").GetProperty("DJMapHelper");
        string djLinked = Path.Combine(Path.GetDirectoryName(request.LinkedAssembly)!, "DJMapHelper.dll");
        if (Path.GetFullPath(dj.GetProperty("fields").GetProperty("LinkedAssembly").GetString()!) != djLinked)
            throw new InvalidDataException("companion linked assembly mapping differs");
        Check(djLinked, dj.GetProperty("inputHashes").GetProperty("LinkedAssembly"), "DJMapHelper linked IL");
        foreach (var pair in new[] { ("LLVMFile", "DJMapHelper.dll.llvm.o"), ("ObjectFile", "DJMapHelper.dll.o"), ("AOTData", "DJMapHelper.aotdata") })
        {
            string expected = Path.Combine(Path.GetDirectoryName(request.LlvmObject)!, pair.Item2);
            if (Path.GetFullPath(dj.GetProperty("fields").GetProperty(pair.Item1).GetString()!) != expected)
                throw new InvalidDataException("companion AOT output mapping differs: " + pair.Item1);
            Check(expected, dj.GetProperty("outputHashes").GetProperty(pair.Item1), "DJMapHelper " + pair.Item1);
        }
        if (Path.GetFullPath(receipt.GetProperty("nativeImage").GetString()!) != Path.GetFullPath(request.NativeImage))
            throw new InvalidDataException("actual native linker output mapping differs");
        Check(request.NativeImage, receipt.GetProperty("nativeImageSha256"), "native image");
        if (Path.GetFullPath(receipt.GetProperty("nativeMain").GetString()!) != Path.GetFullPath(request.NativeMain))
            throw new InvalidDataException("actual native registration source mapping differs");
        Check(request.NativeMain, receipt.GetProperty("nativeMainSha256"), "native registration source");
        string nativeMain = File.ReadAllText(request.NativeMain);
        if (!Regex.IsMatch(nativeMain, @"(?m)^\s*mono_jit_set_aot_mode\s*\(\s*MONO_AOT_MODE_FULL\s*\)\s*;\s*$") ||
            !Regex.IsMatch(nativeMain, @"(?m)^\s*xamarin_supports_dynamic_registration\s*=\s*FALSE\s*;\s*$"))
            throw new InvalidDataException("actual native startup is not static full-AOT without dynamic registration");
        Check(receipt.GetProperty("compiler").GetString()!, receipt.GetProperty("compilerSha256"), "AOT compiler");
        Check(Path.Combine(Path.GetDirectoryName(request.AotProvenance)!, "compiler-items.xml"), receipt.GetProperty("itemsSha256"), "actual compiler items");
        string[] arguments = fields.GetProperty("Arguments").GetString()!.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (fields.GetProperty("Abi").GetString() != "ARM64+LLVM" || fields.GetProperty("Arch").GetString() != "arm64" ||
            !new[] { "full", "static", "asmonly" }.All(arguments.Contains) ||
            !fields.GetProperty("ProcessArguments").GetString()!.Split(' ').Contains("--llvm"))
            throw new InvalidDataException("actual AOT compiler settings are not full/static/LLVM ARM64");
        return new { boundary = "AppleSDK._AOTCompile", freshInputCapturedBeforeCompilation = true,
            sourceCommit = receipt.GetProperty("sourceCommit").GetString(),
            sourceTreeDirty = receipt.GetProperty("sourceTreeDirty").GetBoolean(),
            inputUnchangedAfterCompilation = true, aotOutputsUnchangedAfterNativeLink = true,
            compilerSha256 = receipt.GetProperty("compilerSha256").GetString(),
            receiptSha256 = Hashing.FileSha256(request.AotProvenance) };
    }

    private static object CompareManaged(string linkedPath, string strippedPath)
    {
        using PEReader before = new(File.OpenRead(linkedPath)), after = new(File.OpenRead(strippedPath));
        MetadataReader left = before.GetMetadataReader(), right = after.GetMetadataReader();
        byte[] a = before.GetMetadata().GetContent().ToArray(), b = after.GetMetadata().GetContent().ToArray();
        Dictionary<string, byte[]> sa = Streams(a), sb = Streams(b);
        if (!sa.Keys.Order().SequenceEqual(sb.Keys.Order())) throw new InvalidDataException("stripping changed metadata stream set");
        foreach (string stream in sa.Keys.Where(value => value != "#~"))
            Equal(sa[stream], sb[stream], "metadata stream " + stream);
        byte[] ha = sa["#~"].ToArray(), hb = sb["#~"].ToArray();
        int header = 24 + 4 * System.Numerics.BitOperations.PopCount(BinaryPrimitives.ReadUInt64LittleEndian(ha.AsSpan(8)));
        if (ha[7] != 10 || hb[7] != 1) throw new InvalidDataException("unreviewed Apple metadata tables header transformation");
        ha[7] = hb[7]; Equal(ha[..header], hb[..header], "metadata tables header");
        int methods = 0, initializers = 0;
        foreach (TableIndex table in Enum.GetValues<TableIndex>())
        {
            int count = left.GetTableRowCount(table), size = left.GetTableRowSize(table);
            if (count != right.GetTableRowCount(table) || size != right.GetTableRowSize(table))
                throw new InvalidDataException("stripping changed metadata table shape: " + table);
            if (count == 0) continue;
            int lo = left.GetTableMetadataOffset(table), ro = right.GetTableMetadataOffset(table);
            byte[] x = a.AsSpan(lo, count * size).ToArray(), y = b.AsSpan(ro, count * size).ToArray();
            if (table == TableIndex.CustomAttribute)
            {
                // Preserve duplicates: this is an ordered sequence after a
                // canonical sort, never a set which could discard attributes.
                var xs = Enumerable.Range(0, count).Select(i => Convert.ToHexString(x.AsSpan(i * size, size))).Order().ToArray();
                var ys = Enumerable.Range(0, count).Select(i => Convert.ToHexString(y.AsSpan(i * size, size))).Order().ToArray();
                if (!xs.SequenceEqual(ys)) throw new InvalidDataException("stripping changed custom attributes");
            }
            else if (table == TableIndex.MethodDef)
            {
                for (int i = 0; i < count; i++)
                {
                    int at = i * size; uint rva = BinaryPrimitives.ReadUInt32LittleEndian(x.AsSpan(at));
                    uint targetRva = BinaryPrimitives.ReadUInt32LittleEndian(y.AsSpan(at));
                    if ((rva == 0) != (targetRva == 0)) throw new InvalidDataException("stripping changed MethodDef executable status");
                    ushort flags = BinaryPrimitives.ReadUInt16LittleEndian(x.AsSpan(at + 4));
                    if (BinaryPrimitives.ReadUInt16LittleEndian(y.AsSpan(at + 4)) != (flags | 8))
                        throw new InvalidDataException("stripping changed method implementation flags");
                    Equal(x.AsSpan(at + 6, size - 6).ToArray(), y.AsSpan(at + 6, size - 6).ToArray(), "MethodDef signature/attributes");
                    if (rva != 0) methods++;
                }
            }
            else if (table == TableIndex.FieldRva)
            {
                for (int i = 0; i < count; i++) Equal(x.AsSpan(i * size + 4, size - 4).ToArray(), y.AsSpan(i * size + 4, size - 4).ToArray(), "FieldRVA identity");
                initializers = count;
            }
            else Equal(x, y, "metadata table " + table);
        }
        using AssemblyDefinition original = AssemblyDefinition.ReadAssembly(linkedPath), stripped = AssemblyDefinition.ReadAssembly(strippedPath);
        Dictionary<uint, Mono.Cecil.MethodDefinition> targetMethods = stripped.MainModule.Types.SelectMany(Types).SelectMany(type => type.Methods).ToDictionary(method => method.MetadataToken.ToUInt32());
        foreach (Mono.Cecil.MethodDefinition method in original.MainModule.Types.SelectMany(Types).SelectMany(type => type.Methods).Where(method => method.RVA != 0))
        {
            var changed = targetMethods[method.MetadataToken.ToUInt32()];
            if (!changed.HasBody || changed.Body.Instructions.Count != 1 || changed.Body.Instructions[0].OpCode != OpCodes.Ret ||
                changed.Body.CodeSize != 1 || changed.Body.ExceptionHandlers.Count != 0 || changed.Body.Variables.Count != 0)
                throw new InvalidDataException("stripped executable method is not the reviewed ret stub: " + method.FullName);
        }
        var fields = stripped.MainModule.Types.SelectMany(Types).SelectMany(type => type.Fields).ToDictionary(field => field.MetadataToken.ToUInt32());
        int compared = 0;
        foreach (var field in original.MainModule.Types.SelectMany(Types).SelectMany(type => type.Fields).Where(field => field.RVA != 0))
        { Equal(field.InitialValue, fields[field.MetadataToken.ToUInt32()].InitialValue, "initialized field bytes"); compared++; }
        if (compared != initializers) throw new InvalidDataException("initialized field census differs");
        var lr = before.PEHeaders.CorHeader!.ResourcesDirectory; var rr = after.PEHeaders.CorHeader!.ResourcesDirectory;
        if (lr.Size != rr.Size) throw new InvalidDataException("managed resource size changed");
        if (lr.Size != 0)
            Equal(before.GetSectionData(lr.RelativeVirtualAddress).GetContent(0, lr.Size).ToArray(),
                after.GetSectionData(rr.RelativeVirtualAddress).GetContent(0, rr.Size).ToArray(), "managed resources");
        return new { preservedMethods = left.GetTableRowCount(TableIndex.MethodDef), strippedRetStubs = methods,
            initializedFieldsCompared = compared, allOtherMetadataTablesExact = true, metadataHeapsExact = true,
            customAttributeMultisetExact = true, managedResourcesExact = true };
    }

    private static Dictionary<string, byte[]> Streams(byte[] metadata)
    {
        int offset = (16 + BinaryPrimitives.ReadInt32LittleEndian(metadata.AsSpan(12)) + 3) & ~3;
        int count = BinaryPrimitives.ReadUInt16LittleEndian(metadata.AsSpan(offset + 2)); offset += 4;
        Dictionary<string, byte[]> streams = [];
        for (int i = 0; i < count; i++)
        {
            int start = BinaryPrimitives.ReadInt32LittleEndian(metadata.AsSpan(offset)), size = BinaryPrimitives.ReadInt32LittleEndian(metadata.AsSpan(offset + 4)); offset += 8;
            int end = Array.IndexOf(metadata, (byte)0, offset);
            string name = Encoding.ASCII.GetString(metadata, offset, end - offset); offset = (end + 4) & ~3;
            streams.Add(name, metadata.AsSpan(start, size).ToArray());
        }
        return streams;
    }

    private static object MachO(byte[] bytes)
    {
        uint U(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset));
        ulong Q(int offset) => BinaryPrimitives.ReadUInt64LittleEndian(bytes.AsSpan(offset));
        if (U(0) != 0xfeedfacf || U(4) != 0x100000c || U(12) != 2) throw new InvalidDataException("native product is not an ARM64 Mach-O executable");
        int at = 32; string? uuid = null; List<object> sections = []; List<object> commands = [];
        for (uint i = 0; i < U(16); i++)
        {
            uint command = U(at), size = U(at + 4);
            if (size < 8 || at + size > bytes.Length) throw new InvalidDataException("invalid native load command");
            // Symbol tables and the added signing command are expected to
            // change during strip/sign. Runtime load commands remain exact.
            if (command is not (0x2 or 0xb or 0x1d))
            {
                byte[] normalized = bytes.AsSpan(at, checked((int)size)).ToArray();
                if (command == 0x19 && Encoding.ASCII.GetString(bytes, at + 8, 16).TrimEnd('\0') == "__LINKEDIT")
                {
                    Array.Clear(normalized, 32, 8); // vmsize
                    Array.Clear(normalized, 48, 8); // filesize
                }
                string? payload = null;
                if (command is 0x80000034 or 0x80000033 or 0x26 or 0x29)
                    payload = Hashing.BytesSha256(bytes.AsSpan(checked((int)U(at + 8)), checked((int)U(at + 12))).ToArray());
                commands.Add(new { header = Convert.ToHexString(normalized), payloadSha256 = payload });
            }
            if (command == 0x1b) { if (uuid != null) throw new InvalidDataException("duplicate native UUID"); uuid = Convert.ToHexString(bytes.AsSpan(at + 8, 16)); }
            if (command == 0x19)
                for (uint s = 0; s < U(at + 64); s++)
                {
                    int row = checked(at + 72 + (int)s * 80); uint flags = U(row + 64), offset = U(row + 48); ulong length = Q(row + 40);
                    bool zero = (flags & 0xff) is 1 or 0xc or 0x12;
                    if (!zero && (ulong)offset + length > (ulong)bytes.Length) throw new InvalidDataException("native section exceeds image");
                    sections.Add(new { header = Convert.ToHexString(bytes.AsSpan(row, 80)),
                        contentSha256 = zero ? "ZERO_FILL" : Hashing.BytesSha256(bytes.AsSpan((int)offset, checked((int)length)).ToArray()) });
                }
            at = checked(at + (int)size);
        }
        if (uuid == null || sections.Count == 0) throw new InvalidDataException("native UUID/sections absent");
        return new { architecture = "ARM64", cpuSubtype = U(8), flags = U(24), reserved = U(28), uuid, commands, sections };
    }

    private static Dictionary<string, char> Symbols(string path, IEnumerable<string> required)
    {
        ProcessStartInfo start = new("/usr/bin/nm") { RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add("-P"); start.ArgumentList.Add(path);
        using Process process = Process.Start(start)!;
        string text = process.StandardOutput.ReadToEnd(), error = process.StandardError.ReadToEnd(); process.WaitForExit();
        if (process.ExitCode != 0) throw new InvalidDataException("native symbol inspection failed: " + error.Trim());
        Dictionary<string, char> result = new(StringComparer.Ordinal);
        HashSet<string> selected = required.ToHashSet(StringComparer.Ordinal);
        foreach (string line in text.Split('\n'))
        {
            string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2 && parts[1].Length == 1 && selected.Contains(parts[0]) && !result.TryAdd(parts[0], parts[1][0]))
                throw new InvalidDataException("ambiguous native symbol: " + parts[0]);
        }
        return result;
    }

    private static void RequireNativeDefinitions(string obj, Dictionary<string, char> native, IEnumerable<string> required)
    {
        string[] roots = required.ToArray(); Dictionary<string, char> llvm = Symbols(obj, roots);
        foreach (string root in roots)
            if (!llvm.TryGetValue(root, out char objectKind) || objectKind is not ('T' or 't') ||
                !native.TryGetValue(root, out char nativeKind) || nativeKind is not ('T' or 't'))
                throw new InvalidDataException("selected native root has no exact code definition: " + root);
    }

    private static string Name(string value) => new(value.Where(c => c != '>').Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());
    private static string Symbol(MethodDefinition method) => "_" + Name(method.Module.Assembly.Name.Name) + "_" +
        Name(method.DeclaringType.FullName) + "_" + Name(method.Name) +
        (method.Parameters.Count == 0 ? "" : "_" + string.Join("_", method.Parameters.Select(parameter => Parameter(parameter.ParameterType))));
    private static string Parameter(TypeReference type) => type switch
    {
        ArrayType array when array.Rank == 1 => Parameter(array.ElementType) + "__",
        GenericInstanceType generic => Name(generic.ElementType.FullName) + "_" + string.Join("_", generic.GenericArguments.Select(Parameter)),
        Mono.Cecil.TypeSpecification => throw new InvalidDataException("unreviewed native root signature shape: " + type.FullName),
        _ => type.FullName switch { "System.String" => "string", "System.Single" => "single", "System.Int32" => "int",
            "System.Boolean" => "bool", "System.Char" => "char", "System.Object" => "object", _ => Name(type.FullName) }
    };
    private static int Count(byte[] value, byte[] pattern)
    { int count = 0; for (int i = 0; i <= value.Length - pattern.Length; i++) if (value.AsSpan(i, pattern.Length).SequenceEqual(pattern)) count++; return count; }
    private static IEnumerable<Mono.Cecil.TypeDefinition> Types(Mono.Cecil.TypeDefinition type)
    { yield return type; foreach (var nested in type.NestedTypes.SelectMany(Types)) yield return nested; }
    private static void Equal(byte[] a, byte[] b, string label)
    { if (!a.AsSpan().SequenceEqual(b)) throw new InvalidDataException(label + " differs"); }
    private static void EqualFile(string a, string b, string label)
    { if (Hashing.FileSha256(a) != Hashing.FileSha256(b)) throw new InvalidDataException(label + " differs"); }
}
