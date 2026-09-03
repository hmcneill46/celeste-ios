namespace AppleEverestBuilder;

internal readonly record struct EverestVersion(int Major, int Minor, int Build, int Revision) : IComparable<EverestVersion>
{
    public static EverestVersion Parse(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new InvalidDataException("version is empty");
        string numeric = value.Split('-', 2)[0];
        string[] pieces = numeric.Split('.');
        if (pieces.Length is < 1 or > 4 || pieces.Any(piece => !int.TryParse(piece, out int parsed) || parsed < 0))
            throw new InvalidDataException($"invalid Everest version: {value}");
        int[] parts = pieces.Select(int.Parse).Concat(Enumerable.Repeat(0, 4)).Take(4).ToArray();
        return new EverestVersion(parts[0], parts[1], parts[2], parts[3]);
    }

    // Exact stable-1.6458.0 Everest semantics: matching major, installed
    // minor/build/revision not lower. A 0.0.x installed module is a dev build
    // and intentionally satisfies every requested version.
    public static bool Satisfies(EverestVersion required, EverestVersion installed)
    {
        if (installed.Major == 0 && installed.Minor == 0) return true;
        if (installed.Major != required.Major || installed.Minor < required.Minor) return false;
        if (installed.Minor == required.Minor && installed.Build < required.Build) return false;
        return installed.Minor != required.Minor || installed.Build != required.Build || installed.Revision >= required.Revision;
    }

    public int CompareTo(EverestVersion other)
    {
        int value = Major.CompareTo(other.Major);
        if (value == 0) value = Minor.CompareTo(other.Minor);
        if (value == 0) value = Build.CompareTo(other.Build);
        if (value == 0) value = Revision.CompareTo(other.Revision);
        return value;
    }
}

internal static class EverestGraphResolver
{
    public static IReadOnlyList<ResolvedMod> Resolve(IReadOnlyList<ResolvedMod> entries)
    {
        Dictionary<string, ResolvedMod> byName = new(StringComparer.Ordinal);
        foreach (ResolvedMod mod in entries.OrderBy(item => item.Metadata.Name, StringComparer.Ordinal))
        {
            if (!byName.TryAdd(mod.Metadata.Name, mod))
                throw new InvalidDataException($"duplicate Everest identity: {mod.Metadata.Name}");
        }

        foreach (ResolvedMod mod in byName.Values)
        {
            foreach (EverestDependency conflict in mod.Metadata.Conflicts)
            {
                if (byName.TryGetValue(conflict.Name, out ResolvedMod? installed) &&
                    EverestVersion.Satisfies(EverestVersion.Parse(conflict.Version), EverestVersion.Parse(installed.Metadata.Version)))
                    throw new InvalidDataException($"conflict: {mod.Metadata.Name} rejects {conflict.Name} {conflict.Version}");
            }
            foreach (EverestDependency dependency in RequiredDependencies(mod))
            {
                if (dependency.Name is "Everest" or "EverestCore" or "Celeste")
                {
                    string installed = dependency.Name == "Celeste" ? "1.4.0.0" : "1.6458.0";
                    if (!EverestVersion.Satisfies(EverestVersion.Parse(dependency.Version), EverestVersion.Parse(installed)))
                        throw new InvalidDataException($"incompatible platform dependency: {mod.Metadata.Name} requires {dependency.Name} {dependency.Version}, static host is {installed}");
                    continue;
                }
                RequireCompatible(byName, mod, dependency, optional: false);
            }
            foreach (EverestDependency dependency in OptionalDependencies(mod))
                RequireCompatible(byName, mod, dependency, optional: true);
        }

        Dictionary<string, int> state = new(StringComparer.Ordinal);
        List<ResolvedMod> result = [];
        foreach (string name in byName.Keys.OrderBy(value => value, StringComparer.Ordinal)) Visit(name);
        return result;

        void Visit(string name)
        {
            if (state.TryGetValue(name, out int current))
            {
                if (current == 1) throw new InvalidDataException($"dependency cycle includes {name}");
                if (current == 2) return;
            }
            state[name] = 1;
            ResolvedMod mod = byName[name];
            IEnumerable<string> edges = RequiredDependencies(mod).Select(item => item.Name)
                .Where(item => item is not ("Everest" or "EverestCore" or "Celeste"))
                .Concat(OptionalDependencies(mod).Select(item => item.Name).Where(byName.ContainsKey))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal);
            foreach (string dependency in edges) Visit(dependency);
            state[name] = 2;
            result.Add(mod);
        }
    }

    internal static IReadOnlyList<EverestDependency> RequiredDependencies(ResolvedMod mod) =>
        mod.StaticSemanticLowering?.EffectiveDependencies is { } lowered
            ? lowered.Select(value => new EverestDependency { Name = value.Name, Version = value.Version }).ToArray()
            : mod.Metadata.Dependencies;

    internal static IReadOnlyList<EverestDependency> OptionalDependencies(ResolvedMod mod) =>
        mod.StaticSemanticLowering?.EffectiveDependencies != null
            ? []
            : mod.Metadata.OptionalDependencies;

    private static void RequireCompatible(
        IReadOnlyDictionary<string, ResolvedMod> byName,
        ResolvedMod owner,
        EverestDependency requirement,
        bool optional)
    {
        if (!byName.TryGetValue(requirement.Name, out ResolvedMod? installed))
        {
            if (optional) return;
            throw new InvalidDataException($"missing dependency: {owner.Metadata.Name} requires {requirement.Name} {requirement.Version}");
        }
        if (!EverestVersion.Satisfies(EverestVersion.Parse(requirement.Version), EverestVersion.Parse(installed.Metadata.Version)))
            throw new InvalidDataException($"incompatible dependency: {owner.Metadata.Name} requires {requirement.Name} {requirement.Version}, found {installed.Metadata.Version}");
    }
}
