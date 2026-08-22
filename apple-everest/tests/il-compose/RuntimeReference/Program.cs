using System.Reflection;
using AppleEverest.IlCompose;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using MonoMod.RuntimeDetour.HookGen;

if (args.Length != 1)
    throw new ArgumentException("usage: RuntimeReference scenario");

MethodInfo target = typeof(ComposeTarget).GetMethod(nameof(ComposeTarget.Compose),
    BindingFlags.Public | BindingFlags.Static) ?? throw new MissingMethodException(nameof(ComposeTarget.Compose));
ILContext.Manipulator addThree = SequenceManipulators.AddThree;
ILContext.Manipulator multiplyFive = SequenceManipulators.MultiplyFive;
ILContext.Manipulator subtractSeven = SequenceManipulators.SubtractSeven;

int result = args[0] switch
{
    "event-direct" => WithEventThenDirect(target, addThree, multiplyFive),
    "direct-event" => WithDirectThenEvent(target, multiplyFive, addThree),
    "direct-on" => WithDirectAndOn(target, multiplyFive),
    "event-event-direct-on" => WithEventsDirectAndOn(target, addThree, multiplyFive, subtractSeven),
    _ => throw new InvalidDataException("unknown runtime conformance scenario: " + args[0])
};

Console.WriteLine("APPLE_EVEREST_RUNTIME_SCENARIO=" + args[0]);
Console.WriteLine("APPLE_EVEREST_RUNTIME_RESULT=" + result);

static int WithEventThenDirect(MethodInfo target, ILContext.Manipulator eventManipulator,
    ILContext.Manipulator directManipulator)
{
    Modify(target, eventManipulator);
    try
    {
        using ILHook direct = new(target, directManipulator);
        return ComposeTarget.Compose(4);
    }
    finally
    {
        Unmodify(target, eventManipulator);
    }
}

static int WithDirectThenEvent(MethodInfo target, ILContext.Manipulator directManipulator,
    ILContext.Manipulator eventManipulator)
{
    using ILHook direct = new(target, directManipulator);
    Modify(target, eventManipulator);
    try
    {
        return ComposeTarget.Compose(4);
    }
    finally
    {
        Unmodify(target, eventManipulator);
    }
}

static int WithDirectAndOn(MethodInfo target, ILContext.Manipulator directManipulator)
{
    using ILHook direct = new(target, directManipulator);
    ComposeHook hook = AddHundred;
    Add(target, hook);
    try
    {
        return ComposeTarget.Compose(4);
    }
    finally
    {
        Remove(target, hook);
    }
}

static int WithEventsDirectAndOn(MethodInfo target, ILContext.Manipulator firstEvent,
    ILContext.Manipulator secondEvent, ILContext.Manipulator directManipulator)
{
    Modify(target, firstEvent);
    Modify(target, secondEvent);
    try
    {
        using ILHook direct = new(target, directManipulator);
        ComposeHook hook = AddHundred;
        Add(target, hook);
        try
        {
            return ComposeTarget.Compose(4);
        }
        finally
        {
            Remove(target, hook);
        }
    }
    finally
    {
        Unmodify(target, secondEvent);
        Unmodify(target, firstEvent);
    }
}

static int AddHundred(OrigCompose orig, int value) => orig(value) + 100;

// HookGen-generated On.* events use these exact HookEndpointManager entry
// points. Reflection here only bypasses their deliberate compile-time
// Obsolete(error) annotation in this host conformance program; the pinned
// public runtime methods themselves perform the registration and removal.
static void Modify(MethodBase target, Delegate manipulator) => Endpoint("Modify").Invoke(null, [target, manipulator]);
static void Unmodify(MethodBase target, Delegate manipulator) => Endpoint("Unmodify").Invoke(null, [target, manipulator]);
static void Add(MethodBase target, Delegate hook) => Endpoint("Add").Invoke(null, [target, hook]);
static void Remove(MethodBase target, Delegate hook) => Endpoint("Remove").Invoke(null, [target, hook]);
static MethodInfo Endpoint(string name) => typeof(HookEndpointManager).GetMethods(
        BindingFlags.Public | BindingFlags.Static)
    .Single(method => method.Name == name && !method.IsGenericMethodDefinition &&
        method.GetParameters().Select(parameter => parameter.ParameterType)
            .SequenceEqual([typeof(MethodBase), typeof(Delegate)]));

delegate int OrigCompose(int value);
delegate int ComposeHook(OrigCompose orig, int value);
