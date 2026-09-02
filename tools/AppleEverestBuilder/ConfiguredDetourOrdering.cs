using System.Text.Json;

namespace AppleEverestBuilder;

/// <summary>
/// Host-only implementation of the dependency ordering in the repository-pinned
/// MonoMod.RuntimeDetour DepGraph. The output is an immutable sequence; none of
/// the configuration objects or graph machinery is emitted into an Apple app.
/// </summary>
internal static class ConfiguredDetourOrdering
{
    internal const string SemanticVersion = "pinned-monomod-dfc30a1506d37fb88a2c2be004f525205f46a24c-v1";

    internal static StaticConfiguredDetourSequence Resolve(
        string targetId,
        IEnumerable<StaticConfiguredDetourNode> source)
    {
        StaticConfiguredDetourNode[] nodes = source
            .OrderBy(node => node.ModuleLoadOrdinal)
            .ThenBy(node => node.RegistrationOrdinal)
            .ThenBy(node => node.PlanId, StringComparer.Ordinal)
            .ToArray();
        Validate(targetId, nodes);

        Graph graph = new();
        foreach (StaticConfiguredDetourNode node in nodes.Where(node => node.Config != null))
            graph.Insert(new GraphNode(node));

        string[] registration = nodes.Select(node => node.PlanId).ToArray();
        string[] configured = graph.List().Select(node => node.PlanId).ToArray();
        StaticConfiguredDetourNode[] ordinary = nodes.Where(node => node.Config == null).ToArray();

        // A typed managed dispatcher is assembled inner-to-outer. MonoMod's
        // configured chain executes in graph order before its no-config LIFO
        // chain, hence ordinary registration order followed by graph reverse.
        string[] managedDispatcher = ordinary.Select(node => node.PlanId)
            .Concat(configured.Reverse()).ToArray();

        // IL manipulators are applied to the body sequentially: configured
        // graph order first, then ordinary event registration order.
        string[] ilComposition = configured.Concat(ordinary.Select(node => node.PlanId)).ToArray();
        string hash = PlanHash(targetId, nodes, configured, managedDispatcher, ilComposition);
        return new(targetId, registration, configured, managedDispatcher, ilComposition, hash);
    }

    internal static StaticDetourConfig NormalizeLegacy(
        string id,
        int priority,
        IEnumerable<string> before,
        IEnumerable<string> after,
        uint globalIndex,
        bool forIlHook)
    {
        string[] beforeValues = before.ToArray();
        string[] afterValues = after.ToArray();
        bool beforeAll = beforeValues.Contains("*", StringComparer.Ordinal);
        bool afterAll = afterValues.Contains("*", StringComparer.Ordinal);
        if (beforeAll && afterAll)
            throw new InvalidDataException($"STATIC_CONFIG_IMPOSSIBLE_WILDCARD:{id}");
        if (beforeAll) priority = int.MinValue;
        else if (afterAll) priority = int.MaxValue;

        if (forIlHook)
        {
            priority = unchecked(int.MaxValue - (priority - int.MinValue));
            globalIndex = uint.MaxValue - globalIndex;
            (beforeValues, afterValues) = (afterValues, beforeValues);
        }

        beforeValues = beforeValues.Where(value => value != "*").ToArray();
        afterValues = afterValues.Where(value => value != "*").ToArray();

        // Everest's legacy adapter deliberately swaps Before/After when it
        // constructs the reorganized MonoMod DetourConfig.
        return new StaticDetourConfig(id, priority, afterValues, beforeValues,
            unchecked(int.MinValue + (int)globalIndex));
    }

    private static void Validate(string targetId, StaticConfiguredDetourNode[] nodes)
    {
        if (string.IsNullOrWhiteSpace(targetId))
            throw new InvalidDataException("STATIC_CONFIG_TARGET_REQUIRED");
        if (nodes.Any(node => node.TargetId != targetId))
            throw new InvalidDataException("STATIC_CONFIG_TARGET_MISMATCH:" + targetId);
        if (nodes.Any(node => string.IsNullOrWhiteSpace(node.PlanId)))
            throw new InvalidDataException("STATIC_CONFIG_PLAN_ID_REQUIRED:" + targetId);
        if (nodes.GroupBy(node => node.PlanId, StringComparer.Ordinal).Any(group => group.Count() != 1))
            throw new InvalidDataException("STATIC_CONFIG_DUPLICATE_PLAN:" + targetId);
        if (nodes.GroupBy(node => (node.ModuleLoadOrdinal, node.RegistrationOrdinal)).Any(group => group.Count() != 1))
            throw new InvalidDataException("STATIC_CONFIG_DUPLICATE_REGISTRATION_ORDINAL:" + targetId);
        if (nodes.Any(node => node.ModuleLoadOrdinal < 0 || node.RegistrationOrdinal < 0))
            throw new InvalidDataException("STATIC_CONFIG_NEGATIVE_ORDINAL:" + targetId);
        if (nodes.Any(node => node.Lifetime != "MODULE_IMMUTABLE_ACTIVE"))
            throw new InvalidDataException("DYNAMIC_CONFIG_LIFETIME_DEFERRED:" + targetId);
        if (nodes.Any(node => node.Config is { Id: null or "" }))
            throw new InvalidDataException("STATIC_CONFIG_ID_REQUIRED:" + targetId);
        if (nodes.Any(node => node.Config != null &&
                              (node.Config.Before.Contains("*", StringComparer.Ordinal) ||
                               node.Config.After.Contains("*", StringComparer.Ordinal))))
            throw new InvalidDataException("STATIC_CONFIG_RAW_WILDCARD_REQUIRES_LEGACY_NORMALIZATION:" + targetId);

        foreach (IGrouping<string, StaticConfiguredDetourNode> identity in nodes
                     .Where(node => node.Config != null)
                     .GroupBy(node => node.Config!.Id, StringComparer.Ordinal))
        {
            string[] shapes = identity.Select(node => JsonSerializer.Serialize(node.Config))
                .Distinct(StringComparer.Ordinal).ToArray();
            if (shapes.Length > 1)
                throw new InvalidDataException("STATIC_CONFIG_DUPLICATE_INCOMPATIBLE_ID:" + identity.Key);
        }

        foreach (StaticConfiguredDetourNode node in nodes.Where(node => node.Config != null))
        foreach (string id in node.Config!.Before.Intersect(node.Config.After, StringComparer.Ordinal))
            throw new InvalidDataException($"STATIC_CONFIG_IMPOSSIBLE_CONSTRAINT:{node.PlanId}:{id}");
    }

    private static string PlanHash(
        string targetId,
        StaticConfiguredDetourNode[] nodes,
        string[] configured,
        string[] managed,
        string[] il)
    {
        byte[] json = JsonSerializer.SerializeToUtf8Bytes(new
        {
            semanticVersion = SemanticVersion,
            targetId,
            nodes = nodes.Select(node => new
            {
                node.PlanId,
                node.TargetId,
                node.HookKind,
                config = node.Config == null ? null : new
                {
                    node.Config.Id,
                    node.Config.Priority,
                    before = node.Config.Before,
                    after = node.Config.After,
                    node.Config.SubPriority
                },
                node.RegistrationOrdinal,
                node.ModuleLoadOrdinal,
                node.Lifetime
            }),
            configuredExecutionOrder = configured,
            managedDispatcherOrder = managed,
            ilCompositionOrder = il
        });
        return Hashing.BytesSha256(json);
    }

    private sealed class GraphNode
    {
        internal GraphNode(StaticConfiguredDetourNode value) => Value = value;
        internal StaticConfiguredDetourNode Value { get; }
        internal StaticDetourConfig Config => Value.Config!;
        internal List<GraphNode> BeforeThis { get; } = [];
        internal bool Visiting { get; set; }
        internal bool Visited { get; set; }
    }

    private sealed class Graph
    {
        private readonly List<GraphNode> nodes = [];

        internal void Insert(GraphNode node)
        {
            int insertIndex = -1;
            for (int index = 0; index < nodes.Count; index++)
            {
                GraphNode current = nodes[index];
                current.Visited = false;
                if (insertIndex < 0 && node.Config.Priority is int priority)
                {
                    if (current.Config.Priority is int currentPriority)
                    {
                        if (priority >= currentPriority) insertIndex = index;
                    }
                    else insertIndex = index;
                }

                bool isBefore = false;
                bool isAfter = false;
                if (node.Config.Before.Contains(current.Config.Id, StringComparer.Ordinal))
                {
                    PriorityInsert(current.BeforeThis, node);
                    isBefore = true;
                }
                if (node.Config.After.Contains(current.Config.Id, StringComparer.Ordinal))
                {
                    if (isBefore) throw Impossible(node, current);
                    PriorityInsert(node.BeforeThis, current);
                    isAfter = true;
                }
                if (current.Config.Before.Contains(node.Config.Id, StringComparer.Ordinal))
                {
                    if (isBefore) throw Impossible(node, current);
                    PriorityInsert(node.BeforeThis, current);
                    isAfter = true;
                }
                if (current.Config.After.Contains(node.Config.Id, StringComparer.Ordinal))
                {
                    if (isAfter) throw Impossible(node, current);
                    PriorityInsert(current.BeforeThis, node);
                }
            }

            if (insertIndex < 0) insertIndex = nodes.Count;
            else
            {
                for (; insertIndex < nodes.Count; insertIndex++)
                {
                    GraphNode current = nodes[insertIndex];
                    if (current.Config.Priority != node.Config.Priority ||
                        current.Config.SubPriority <= node.Config.SubPriority) break;
                }
            }
            nodes.Insert(insertIndex, node);
            _ = List(); // Reject a cycle at the construction site, before AOT.
        }

        internal IReadOnlyList<StaticConfiguredDetourNode> List()
        {
            foreach (GraphNode node in nodes)
            {
                node.Visited = false;
                node.Visiting = false;
            }
            List<StaticConfiguredDetourNode> result = [];
            foreach (GraphNode node in nodes) InsertListNode(result, node);
            return result;
        }

        private static void PriorityInsert(List<GraphNode> list, GraphNode node)
        {
            if (node.Config.Priority is not int priority)
            {
                list.Add(node);
                return;
            }
            int insertIndex = -1;
            for (int index = 0; index < list.Count; index++)
            {
                GraphNode current = list[index];
                if (current.Config.Priority is int currentPriority)
                {
                    if (priority >= currentPriority) { insertIndex = index; break; }
                }
                else { insertIndex = index; break; }
            }
            if (insertIndex < 0) insertIndex = list.Count;
            else
            {
                for (; insertIndex < list.Count; insertIndex++)
                {
                    GraphNode current = list[insertIndex];
                    if (current.Config.Priority != node.Config.Priority ||
                        current.Config.SubPriority <= node.Config.SubPriority) break;
                }
            }
            list.Insert(insertIndex, node);
        }

        private static void InsertListNode(List<StaticConfiguredDetourNode> result, GraphNode node)
        {
            if (node.Visiting) throw new InvalidDataException("STATIC_CONFIG_ORDER_CYCLE");
            if (node.Visited) return;
            node.Visiting = true;
            try
            {
                foreach (GraphNode before in node.BeforeThis) InsertListNode(result, before);
                result.Add(node.Value);
                node.Visited = true;
            }
            finally { node.Visiting = false; }
        }

        private static InvalidDataException Impossible(GraphNode left, GraphNode right) =>
            new($"STATIC_CONFIG_IMPOSSIBLE_CONSTRAINT:{left.Value.PlanId}:{right.Value.PlanId}");
    }
}
