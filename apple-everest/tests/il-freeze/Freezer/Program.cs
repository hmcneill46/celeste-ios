using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;

if (args.Length != 2)
    throw new ArgumentException("usage: Freezer input.dll output.dll");

using ModuleDefinition module = ModuleDefinition.ReadModule(args[0]);
MethodDefinition method = module.Types.Single(type => type.FullName == "AppleEverest.IlFreeze.CanaryTarget")
    .Methods.Single(candidate => candidate.Name == "Scale");

static void CanaryManipulator(ILContext il)
{
    ILCursor cursor = new(il);
    if (!cursor.TryGotoNext(MoveType.Before, instruction => instruction.MatchLdcI4(2)))
        throw new InvalidOperationException("expected ldc.i4.2 not found");
    cursor.Next.OpCode = OpCodes.Ldc_I4_3;
}

using (ILContext context = new(method))
    context.Invoke(CanaryManipulator);

module.Write(args[1]);
Console.WriteLine("PASS: pinned MonoMod IL manipulator frozen into target IL");
