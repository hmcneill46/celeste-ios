using System;
using Mono.Cecil.Cil;
using MonoMod.Cil;

namespace AppleEverest.IlCompose;

public static class SequenceManipulators
{
    // A: (value * 2) + 3
    public static void AddThree(ILContext il)
    {
        ILCursor cursor = new(il);
        cursor.GotoNext(MoveType.Before, instruction => instruction.MatchRet());
        cursor.Emit(OpCodes.Ldc_I4_3);
        cursor.Emit(OpCodes.Add);
    }

    // B: (current result) * 5. A then B differs observably from B then A.
    public static void MultiplyFive(ILContext il)
    {
        ILCursor cursor = new(il);
        cursor.GotoNext(MoveType.Before, instruction => instruction.MatchRet());
        cursor.Emit(OpCodes.Ldc_I4_5);
        cursor.Emit(OpCodes.Mul);
    }

    // C: (current result) - 7. A then B then C exercises a 3-step chain.
    public static void SubtractSeven(ILContext il)
    {
        ILCursor cursor = new(il);
        cursor.GotoNext(MoveType.Before, instruction => instruction.MatchRet());
        cursor.Emit(OpCodes.Ldc_I4_7);
        cursor.Emit(OpCodes.Sub);
    }

    public static void SingletonAddEleven(ILContext il)
    {
        ILCursor cursor = new(il);
        cursor.GotoNext(MoveType.Before, instruction => instruction.MatchRet());
        cursor.EmitDelegate<Func<int, int>>(value => value + 11);
    }

    public static void DanglingBranch(ILContext il)
    {
        ILCursor cursor = new(il);
        cursor.GotoNext(MoveType.Before, instruction => instruction.MatchRet());
        cursor.Emit(OpCodes.Br, Instruction.Create(OpCodes.Nop));
    }

    public static void NoOp(ILContext il)
    {
        _ = il;
    }
}
