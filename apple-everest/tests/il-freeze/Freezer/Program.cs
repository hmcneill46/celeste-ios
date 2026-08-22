using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;

if (args.Length != 2)
    throw new ArgumentException("usage: Freezer input.dll output.dll");

using ModuleDefinition module = ModuleDefinition.ReadModule(args[0]);
TypeDefinition target = module.Types.Single(type => type.FullName == "AppleEverest.IlFreeze.CanaryTarget");
MethodDefinition Method(string name) => target.Methods.Single(candidate => candidate.Name == name);

static void Invoke(MethodDefinition method, ILContext.Manipulator manipulator)
{
    using ILContext context = new(method);
    context.Invoke(manipulator);
}

static void CanaryManipulator(ILContext il)
{
    ILCursor cursor = new(il);
    if (!cursor.TryGotoNext(MoveType.Before, instruction => instruction.MatchLdcI4(2)))
        throw new InvalidOperationException("expected ldc.i4.2 not found");
    cursor.Next.OpCode = OpCodes.Ldc_I4_3;
}

Invoke(Method("Scale"), CanaryManipulator);

MethodDefinition bump = Method("Bump");
Invoke(Method("StaticCall"), il =>
{
    ILCursor cursor = new(il);
    cursor.GotoNext(MoveType.Before, instruction => instruction.MatchRet());
    cursor.Emit(OpCodes.Call, bump);
});

Invoke(Method("Branch"), il =>
{
    ILCursor cursor = new(il);
    ILLabel original = cursor.DefineLabel();
    cursor.Emit(OpCodes.Ldarg_0);
    cursor.Emit(OpCodes.Brtrue, original);
    cursor.Emit(OpCodes.Ldc_I4, 99);
    cursor.Emit(OpCodes.Ret);
    cursor.MarkLabel(original);
});

Invoke(Method("AlterReturn"), il =>
{
    ILCursor cursor = new(il);
    cursor.GotoNext(MoveType.Before, instruction => instruction.MatchRet());
    cursor.Emit(OpCodes.Ldc_I4_4);
    cursor.Emit(OpCodes.Add);
});

Invoke(Method("LocalRoundTrip"), il =>
{
    VariableDefinition local = new(module.TypeSystem.Int32);
    il.Body.Variables.Add(local);
    il.Body.InitLocals = true;
    ILCursor cursor = new(il);
    cursor.GotoNext(MoveType.Before, instruction => instruction.MatchRet());
    cursor.Emit(OpCodes.Stloc, local);
    cursor.Emit(OpCodes.Ldloc, local);
});

Invoke(Method("RemoveRoundTrip"), il =>
{
    ILCursor cursor = new(il);
    cursor.Emit(OpCodes.Nop);
    cursor.Emit(OpCodes.Nop);
    cursor.Goto(0);
    cursor.RemoveRange(2);
});

module.Write(args[1]);
Console.WriteLine("PASS: pinned MonoMod IL conformance matrix frozen into target IL");
