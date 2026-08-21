using MonoMod.ModInterop;

static class Check
{
    internal static int Count;
    internal static void Equal<T>(T expected, T actual, string name)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{name}: expected={expected} actual={actual}");
        Count++;
    }
}

[ModExportName("Shared")]
public static class ProviderA
{
    public static string Echo(string value) => "A:" + value;
    public static void Signal(int value) => State.Last = "A:" + value;
}

[ModExportName("Shared")]
public static class ProviderB
{
    public static string Echo(string value) => "B:" + value;
    public static void Signal(int value) => State.Last = "B:" + value;
}

[ModImportName("Shared")]
public static class ImportsBefore
{
    public static Func<string, string>? Echo;
    public static Action<int>? Signal;
    public static Func<int, string>? Missing;
}

[ModImportName("Shared")]
public static class ImportsAfter
{
    public static Func<string, string>? Echo;
}

[ModExportName("Over")]
public static class OverloadProvider
{
    public static int Convert(int value) => value + 100;
    public static string Convert(string value) => "string:" + value;
    public static object Variant(object value) => "object:" + value;
    public static string Covariant() => "covariant";
}

[ModImportName("Over")]
public static class OverloadImports
{
    public static Func<string, string>? Convert;
    public static Func<string, object>? Variant;
    public static Func<object>? Covariant;
}

public delegate int RefOperation(ref int value);

[ModExportName("Typed")]
public static class TypedProvider
{
    public static int Bump(ref int value) => ++value;
    public static int Count(List<string> values) => values.Count;
}

[ModImportName("Typed")]
public static class TypedImports
{
    public static RefOperation? Bump;
    public static Func<List<string>, int>? Count;
}

public static class DefaultProvider
{
    public static string Default(string value) => "default:" + value;
    public static string Plain(string value) => "plain:" + value;
}

public static class DefaultImports
{
    [ModImportName("AppleEverestDesktopModInteropReference.Default")]
    public static Func<string, string>? Qualified;
    [ModImportName("Plain")]
    public static Func<string, string>? Unqualified;
}

[ModExportName("Reverse")]
public static class ReverseA { public static string Pick() => "A"; }
[ModExportName("Reverse")]
public static class ReverseB { public static string Pick() => "B"; }
[ModImportName("Reverse")]
public static class ReverseImports { public static Func<string>? Pick; }

public static class State { public static string Last = ""; }

public static class Program
{
public static void Main()
{
typeof(ImportsBefore).ModInterop();
Check.Equal<Func<string, string>?>(null, ImportsBefore.Echo, "import before provider is null");
typeof(ProviderA).ModInterop();
Check.Equal("A:x", ImportsBefore.Echo!("x"), "import refresh after provider");
ImportsBefore.Echo = value => "manual:" + value;
typeof(ProviderA).ModInterop();
Check.Equal("manual:x", ImportsBefore.Echo!("x"), "duplicate registration is no-op");
typeof(ProviderB).ModInterop();
typeof(ImportsAfter).ModInterop();
Check.Equal("A:x", ImportsAfter.Echo!("x"), "first provider registration wins");
Check.Equal<Func<int, string>?>(null, ImportsBefore.Missing, "missing import remains null");
ImportsBefore.Signal!(7);
Check.Equal("A:7", State.Last, "Action import");

typeof(OverloadImports).ModInterop();
typeof(OverloadProvider).ModInterop();
Check.Equal("string:x", OverloadImports.Convert!("x"), "incompatible overload skipped");
Check.Equal("object:x", OverloadImports.Variant!("x"), "contravariant parameter accepted");
Check.Equal("covariant", OverloadImports.Covariant!(), "covariant return accepted");

typeof(TypedProvider).ModInterop();
typeof(TypedImports).ModInterop();
int number = 4;
Check.Equal(5, TypedImports.Bump!(ref number), "custom ref delegate");
Check.Equal(2, TypedImports.Count!(new List<string> { "a", "b" }), "closed generic delegate");

typeof(DefaultImports).ModInterop();
typeof(DefaultProvider).ModInterop();
Check.Equal("default:x", DefaultImports.Qualified!("x"), "assembly prefix");
Check.Equal("plain:x", DefaultImports.Unqualified!("x"), "unqualified export");

typeof(ReverseB).ModInterop();
typeof(ReverseA).ModInterop();
typeof(ReverseImports).ModInterop();
Check.Equal("B", ReverseImports.Pick!(), "reverse registration order");

Console.WriteLine($"PASS: pinned MonoMod ModInterop reference ({Check.Count})");
}
}
