using Mono.Cecil;
using Mono.Cecil.Cil;

namespace AppleEverestBuilder;

// Host-only negative inputs. The normal worker must reject these byte changes
// against its reviewed target baseline before it can write a device assembly.
internal static class SidewaysControlInputs
{
    internal static void Write(string source, string output)
    {
        Directory.CreateDirectory(output);
        foreach (string kind in new[] { "collision-site", "branch-boundary" })
        {
            using var assembly = AssemblyDefinition.ReadAssembly(source);
            var method = assembly.MainModule.GetType("Celeste.Player").Methods.Single(m => m.Name == "WallJumpCheck");
            if (kind == "collision-site")
            {
                var call = method.Body.Instructions.First(i => i.Operand is GenericInstanceMethod m &&
                    m.Name == "CollideCheck" && m.GenericArguments.Any(t => t.FullName == "Celeste.Solid"));
                call.OpCode = OpCodes.Nop; call.Operand = null;
            }
            else
            {
                var branch = method.Body.Instructions.First(i => i.Operand is Instruction target &&
                    target != method.Body.Instructions[0]);
                branch.Operand = method.Body.Instructions[0];
            }
            assembly.Write(Path.Combine(output, kind + ".dll"));
        }
    }
}
