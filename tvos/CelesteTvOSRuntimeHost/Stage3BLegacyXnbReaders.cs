using System.Runtime.CompilerServices;

namespace Microsoft.Xna.Framework.Content;

/// <summary>
/// Registers the one validated Celeste XNB reader spelling that cannot be
/// resolved by modern .NET: its generic argument still names mscorlib.
/// This compiles into FNA only for the Stage 3B project graph, giving AOT a
/// direct constructor call and leaving the pinned FNA source untouched.
/// </summary>
internal static class Stage3BLegacyXnbReaders
{
    private const string LegacyCharListReader =
        "Microsoft.Xna.Framework.Content.ListReader`1[[System.Char, mscorlib, Version=4.0.0.0, " +
        "Culture=neutral, PublicKeyToken=b77a5c561934e089]]";

    [ModuleInitializer]
    internal static void Register()
    {
        ContentTypeReaderManager.AddTypeCreator(LegacyCharListReader, static () => new ListReader<char>());
    }
}
