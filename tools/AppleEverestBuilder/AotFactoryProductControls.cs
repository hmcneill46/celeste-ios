using System.Buffers.Binary;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Mono.Cecil;
using AssemblyDefinition = Mono.Cecil.AssemblyDefinition;

namespace AppleEverestBuilder;

internal static partial class AotFactoryProductInspection
{
    // Positive authority is the actual product. Corrupt only disposable copies;
    // each control must fail at its intended gate, not an unrelated missing file.
    internal static void WriteControls(string requestPath, string manifest, string profiles, string output)
    {
        Request request = JsonSerializer.Deserialize<Request>(File.ReadAllText(requestPath))!;
        string scratch = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(output))!, "aot-controls-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(scratch);
        List<object> results = [];
        try
        {
            Write(requestPath, manifest, profiles, Path.Combine(scratch, "positive.json"));
            void Reject(string name, string expected, Action action)
            {
                bool rejected = false;
                try { action(); }
                catch (InvalidDataException error) when (error.Message.Contains(expected, StringComparison.Ordinal)) { rejected = true; }
                if (!rejected) throw new InvalidDataException("AOT negative control failed at intended gate: " + name);
                results.Add(new { name, rejected = true, gate = expected });
            }
            string Changed(string source, Action<byte[]> mutate)
            {
                byte[] bytes = File.ReadAllBytes(source); mutate(bytes);
                string path = Path.Combine(scratch, "mutation-" + Guid.NewGuid().ToString("N") + Path.GetExtension(source));
                File.WriteAllBytes(path, bytes); return path;
            }
            using AssemblyDefinition linked = AssemblyDefinition.ReadAssembly(request.LinkedAssembly);
            using PEReader original = new(File.OpenRead(request.LinkedAssembly)), stripped = new(File.OpenRead(request.StrippedAssembly));
            int RvaOffset(PEReader pe, int rva)
            {
                var section = pe.PEHeaders.SectionHeaders.Single(section => rva >= section.VirtualAddress && rva < section.VirtualAddress + section.VirtualSize);
                return section.PointerToRawData + rva - section.VirtualAddress;
            }
            string wrongMvid = Changed(request.StrippedAssembly, bytes =>
            {
                int at = bytes.AsSpan().IndexOf(linked.MainModule.Mvid.ToByteArray());
                if (at < 0) throw new InvalidDataException("control MVID not located"); bytes[at] ^= 1;
            });
            Reject("WRONG_STRIPPED_MVID", "metadata stream #GUID", () => CompareManaged(request.LinkedAssembly, wrongMvid));
            int table = RvaOffset(stripped, stripped.PEHeaders.CorHeader!.MetadataDirectory.RelativeVirtualAddress) +
                stripped.GetMetadataReader().GetTableMetadataOffset(TableIndex.CustomAttribute);
            string attributes = Changed(request.StrippedAssembly, bytes => bytes[table + 2] ^= 1);
            Reject("CHANGED_CUSTOM_ATTRIBUTE", "custom attributes", () => CompareManaged(request.LinkedAssembly, attributes));
            using AssemblyDefinition strippedAssembly = AssemblyDefinition.ReadAssembly(request.StrippedAssembly);
            var field = strippedAssembly.MainModule.Types.SelectMany(Types).SelectMany(type => type.Fields).First(field => field.RVA != 0);
            string initialized = Changed(request.StrippedAssembly, bytes => bytes[RvaOffset(stripped, field.RVA)] ^= 1);
            Reject("CHANGED_INITIALIZED_FIELD", "initialized field bytes", () => CompareManaged(request.LinkedAssembly, initialized));
            string data = Changed(request.PackagedAotData, bytes => bytes[^1] ^= 1);
            Reject("CHANGED_PACKAGED_AOT_DATA", "packaged AOT data", () => EqualFile(request.AotData, data, "packaged AOT data"));

            var update = linked.MainModule.GetType("Celeste.Mod.AppleEverestWindPetals").Methods.Single(method => method.Name == "Update");
            string changedIl = Changed(request.LinkedAssembly, bytes =>
            {
                int at = RvaOffset(original, update.RVA);
                int header = (bytes[at] & 3) == 2 ? 1 : (BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(at)) >> 12) * 4;
                bytes[at + header] ^= 1;
            });
            // Relocate only the request mapping to the disposable control;
            // preserve the actual before-AOT content hash and original MVID.
            JsonNode receipt = JsonNode.Parse(File.ReadAllText(request.AotProvenance))!;
            receipt["fields"]!["LinkedAssembly"] = changedIl;
            string controlReceipt = Path.Combine(scratch, "receipt.json"); File.WriteAllText(controlReceipt, receipt.ToJsonString());
            Reject("CHANGED_SELECTED_UPDATE_WITH_SAME_MVID_STALE_AOT", "AOT provenance content changed: linked IL",
                () => VerifyProvenance(request with { LinkedAssembly = changedIl, AotProvenance = controlReceipt }));
            string changedMain = Changed(request.NativeMain, bytes => bytes[^1] ^= 1);
            receipt = JsonNode.Parse(File.ReadAllText(request.AotProvenance))!;
            receipt["nativeMain"] = changedMain;
            File.WriteAllText(controlReceipt, receipt.ToJsonString());
            Reject("CHANGED_NATIVE_REGISTRATION_SOURCE", "AOT provenance content changed: native registration source",
                () => VerifyProvenance(request with { NativeMain = changedMain, AotProvenance = controlReceipt }));
            receipt = JsonNode.Parse(File.ReadAllText(request.AotProvenance))!;
            receipt["phase"] = "BEFORE_AOT";
            File.WriteAllText(controlReceipt, receipt.ToJsonString());
            Reject("INCOMPLETE_COMPILER_BOUNDARY_RECEIPT", "actual AOT compiler boundary receipt absent",
                () => VerifyProvenance(request with { AotProvenance = controlReceipt }));

            uint U(byte[] bytes, int at) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(at));
            int Command(byte[] bytes, uint kind)
            { int at = 32; for (uint i = 0; i < U(bytes, 16); i++) { if (U(bytes, at) == kind) return at; at += checked((int)U(bytes, at + 4)); } throw new InvalidDataException("control native load command absent"); }
            void NativeControl(string name, Action<byte[]> mutate)
            {
                string changed = Changed(request.PackagedNativeImage, mutate);
                Reject(name, "native binding", () =>
                { if (JsonSerializer.Serialize(MachO(File.ReadAllBytes(request.NativeImage))) != JsonSerializer.Serialize(MachO(File.ReadAllBytes(changed)))) throw new InvalidDataException("native binding"); });
            }
            NativeControl("CHANGED_NATIVE_ENTRYPOINT", bytes => bytes[Command(bytes, 0x80000028) + 8] ^= 4);
            NativeControl("CHANGED_CHAINED_FIXUPS", bytes => bytes[checked((int)U(bytes, Command(bytes, 0x80000034) + 8))] ^= 1);
            NativeControl("CHANGED_NATIVE_CODE_SECTION", bytes =>
            {
                int at = 32;
                for (uint i = 0; i < U(bytes, 16); i++)
                {
                    if (U(bytes, at) == 0x19) for (uint s = 0; s < U(bytes, at + 64); s++)
                    {
                        int row = checked(at + 72 + (int)s * 80);
                        if (Encoding.ASCII.GetString(bytes, row, 16).TrimEnd('\0') == "__text")
                        { bytes[checked((int)U(bytes, row + 48))] ^= 1; return; }
                    }
                    at += checked((int)U(bytes, at + 4));
                }
                throw new InvalidDataException("control native text section absent");
            });
            string root = "_Celeste_Celeste_Mod_AppleEverestSelectedProfileGuard_Matches_System_Collections_Generic_Dictionary_2_string_object_System_Collections_Generic_Dictionary_2_string_object";
            var native = Symbols(request.NativeImage, [root]);
            RequireNativeDefinitions(request.LlvmObject, native, [root]);
            string undefined = Changed(request.LlvmObject, bytes =>
            {
                int command = Command(bytes, 2), symbols = checked((int)U(bytes, command + 8)), strings = checked((int)U(bytes, command + 16));
                for (uint i = 0; i < U(bytes, command + 12); i++)
                {
                    int row = checked(symbols + (int)i * 16), name = checked(strings + (int)U(bytes, row));
                    int end = Array.IndexOf(bytes, (byte)0, name);
                    if (Encoding.ASCII.GetString(bytes, name, end - name) != root) continue;
                    bytes[row + 4] = 1; bytes[row + 5] = 0; Array.Clear(bytes, row + 8, 8); return;
                }
                throw new InvalidDataException("control native definition absent");
            });
            Reject("UNDEFINED_GUARD_REFERENCE_IS_NOT_CODE", "no exact code definition", () => RequireNativeDefinitions(undefined, native, [root]));
            string missing = Changed(request.LlvmObject, bytes =>
            { int at = bytes.AsSpan().IndexOf(Encoding.ASCII.GetBytes(root + "\0")); if (at < 0) throw new InvalidDataException("control symbol absent"); bytes[at + 1] = (byte)'X'; });
            Reject("MISSING_GUARD_HELPER_CODE", "no exact code definition", () => RequireNativeDefinitions(missing, native, [root]));
            using var contract = SelectedFactoryContract.Load(manifest, profiles);
            if (contract.IsSnas)
            {
                // This constructor belongs only to the separate legacy six,
                // not the selected77 closure. Require it in the actual root
                // inventory before testing its disposable native omission.
                var constructor = linked.MainModule.GetType("Celeste.Mod.AppleEverestStage25KERootCanary")
                    .Methods.Single(method => method.IsConstructor && !method.IsStatic);
                string legacyRoot = Symbol(constructor);
                using JsonDocument positive = JsonDocument.Parse(File.ReadAllText(Path.Combine(scratch, "positive.json")));
                if (!positive.RootElement.GetProperty("exactNativeRoots").EnumerateArray().Any(value => value.GetString() == legacyRoot))
                    throw new InvalidDataException("separate legacy constructor omitted from actual required native roots");
                var legacyNative = Symbols(request.NativeImage, [legacyRoot]);
                RequireNativeDefinitions(request.LlvmObject, legacyNative, [legacyRoot]);
                string missingLegacy = Changed(request.LlvmObject, bytes =>
                {
                    int at = bytes.AsSpan().IndexOf(Encoding.ASCII.GetBytes(legacyRoot + "\0"));
                    if (at < 0) throw new InvalidDataException("legacy control symbol absent");
                    bytes[at + 1] = (byte)'X';
                });
                Reject("MISSING_SEPARATE_LEGACY_CONSTRUCTOR_CODE", "no exact code definition",
                    () => RequireNativeDefinitions(missingLegacy, legacyNative, [legacyRoot]));
                var audioName = linked.MainModule.GetType("Celeste.Mod.AppleEverestCustomAudioRuntime")
                    .Methods.Single(method => method.Name == "GetEventName");
                string audioRoot = Symbol(audioName);
                if (!positive.RootElement.GetProperty("exactNativeRoots").EnumerateArray().Any(value => value.GetString() == audioRoot))
                    throw new InvalidDataException("audio name resolver omitted from required native roots");
                var audioNative = Symbols(request.NativeImage, [audioRoot]);
                RequireNativeDefinitions(request.LlvmObject, audioNative, [audioRoot]);
                string missingAudio = Changed(request.LlvmObject, bytes =>
                {
                    int at = bytes.AsSpan().IndexOf(Encoding.ASCII.GetBytes(audioRoot + "\0"));
                    if (at < 0) throw new InvalidDataException("audio name control symbol absent");
                    bytes[at + 1] = (byte)'X';
                });
                Reject("MISSING_AUDIO_NAME_RESOLVER_CODE", "no exact code definition",
                    () => RequireNativeDefinitions(missingAudio, audioNative, [audioRoot]));
                var audioOut = linked.MainModule.GetType("Celeste.Mod.AppleEverestCustomAudioRuntime")
                    .Methods.Single(method => method.Name == "TryGetEventDescription");
                string audioOutRoot = Symbol(audioOut);
                if (!positive.RootElement.GetProperty("exactNativeRoots").EnumerateArray().Any(value => value.GetString() == audioOutRoot)
                    || audioOut.Parameters[^1].ParameterType is not ByReferenceType || !audioOutRoot.EndsWith('_'))
                    throw new InvalidDataException("audio out-parameter method omitted from actual native roots");
                var audioOutNative = Symbols(request.NativeImage, [audioOutRoot]);
                RequireNativeDefinitions(request.LlvmObject, audioOutNative, [audioOutRoot]);
                string missingOut = Changed(request.LlvmObject, bytes =>
                {
                    int at = bytes.AsSpan().IndexOf(Encoding.ASCII.GetBytes(audioOutRoot + "\0"));
                    if (at < 0) throw new InvalidDataException("audio out-parameter control symbol absent");
                    bytes[at + 1] = (byte)'X';
                });
                Reject("MISSING_AUDIO_OUT_PARAMETER_CODE", "no exact code definition",
                    () => RequireNativeDefinitions(missingOut, audioOutNative, [audioOutRoot]));
                // A by-value spelling cannot stand in for the compiled byref
                // method. Invoke the real object/native code-definition gate.
                string byValueRoot = audioOutRoot[..^1];
                Reject("VALUE_PARAMETER_CANNOT_SATISFY_BYREF_ROOT", "no exact code definition",
                    () => RequireNativeDefinitions(request.LlvmObject,
                        Symbols(request.NativeImage, [byValueRoot]), [byValueRoot]));
                string legacyMethod = Changed(request.LinkedAssembly, bytes =>
                {
                    var updateLegacy = linked.MainModule.GetType("Celeste.Mod.AppleEverestStage25KERootCanary")
                        .Methods.Single(method => method.Name == "Update");
                    int at = RvaOffset(original, updateLegacy.RVA);
                    int header = (bytes[at] & 3) == 2 ? 1 : (BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(at)) >> 12) * 4;
                    bytes[at + header] ^= 1;
                });
                receipt = JsonNode.Parse(File.ReadAllText(request.AotProvenance))!;
                receipt["fields"]!["LinkedAssembly"] = legacyMethod;
                File.WriteAllText(controlReceipt, receipt.ToJsonString());
                Reject("CHANGED_LEGACY_UPDATE_WITH_STALE_AOT", "AOT provenance content changed: linked IL",
                    () => VerifyProvenance(request with { LinkedAssembly = legacyMethod, AotProvenance = controlReceipt }));
            }
            File.WriteAllText(output, JsonSerializer.Serialize(new { schemaVersion = 1, actualProductPositive = true,
                disposableCopiesOnly = true, controls = results }, new JsonSerializerOptions { WriteIndented = true }) + "\n");
        }
        finally { Directory.Delete(scratch, recursive: true); }
    }
}
