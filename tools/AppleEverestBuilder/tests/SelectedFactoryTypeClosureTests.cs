using System.Text.Json;
using AppleEverestBuilder;

internal static class SelectedFactoryTypeClosureTests
{
    internal static int Run()
    {
        int passed = 0;
        void Pass(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException("FAIL: " + name);
            passed++;
        }

        SelectedFactoryClosureManifest Valid()
        {
            SelectedFactoryClosureNode[] nodes = SelectedFactoryTypeClosure.RequiredDimensions
                .Select(dimension => new SelectedFactoryClosureNode
                {
                    Id = dimension.ToLowerInvariant(), Kind = dimension, Required = true,
                    Classification = "ACCEPTED_STATIC_RUNTIME", Evidence = "synthetic exact evidence"
                }).ToArray();
            return new SelectedFactoryClosureManifest
            {
                SchemaVersion = 1,
                Profile = "synthetic",
                Nodes = nodes,
                Factories =
                [
                    new SelectedFactoryClosureFactory
                    {
                        Kind = "entity", CustomId = "Fixture/Entity", Provider = "Fixture",
                        ProviderAssembly = "Fixture.dll", ConcreteSourceType = "Fixture.Entity",
                        BaseChain = ["Fixture.Entity", "Celeste.Entity", "Monocle.Entity"],
                        Constructor = ".ctor(EntityData,Vector2)",
                        ConstructorParameterTypes = ["Celeste.EntityData", "Microsoft.Xna.Framework.Vector2"],
                        ReachableLifecycleMethods = ["Added", "Awake", "Update", "Render", "Removed", "SceneEnd"],
                        RequiredModuleLoadBehavior = ["none"], RequiredHookSites = ["none"],
                        RequiredReflectionSites = ["none"], ContentRequirements = ["none"],
                        Classification = "ACCEPTED_STATIC_RUNTIME",
                        RootNodeIds = nodes.Select(node => node.Id).ToArray()
                    }
                ]
            };
        }

        SelectedFactoryClosureManifest Clone(SelectedFactoryClosureManifest value) =>
            JsonSerializer.Deserialize<SelectedFactoryClosureManifest>(JsonSerializer.Serialize(value))!;

        bool Rejects(SelectedFactoryClosureManifest value, string token)
        {
            SelectedFactoryClosureResult result = SelectedFactoryTypeClosure.Validate(value);
            return result.FullyClosed == 0 && result.Violations.Any(message =>
                message.Contains(token, StringComparison.OrdinalIgnoreCase));
        }

        Pass(SelectedFactoryTypeClosure.Validate(Valid()) is { FullyClosed: 1, Blocked: 0, Unknown: 0 },
            "complete selected factory closure accepted");

        SelectedFactoryClosureManifest missingDirectBase = Clone(Valid());
        missingDirectBase.Nodes.Single(node => node.Kind == "BASE_CHAIN").Classification = "UNKNOWN";
        Pass(Rejects(missingDirectBase, "BASE_CHAIN"), "missing direct base class rejected");

        SelectedFactoryClosureManifest missingTransitiveBase = Clone(Valid());
        SelectedFactoryClosureNode baseNode = missingTransitiveBase.Nodes.Single(node => node.Kind == "BASE_CHAIN");
        baseNode.Dependencies = ["missing-transitive-base"];
        Pass(Rejects(missingTransitiveBase, "missing-transitive-base"), "missing transitive base class rejected");

        SelectedFactoryClosureManifest missingConstructorParameter = Clone(Valid());
        missingConstructorParameter.Nodes.Single(node => node.Kind == "CONSTRUCTOR_PARAMETER_TYPES").Classification = "UNKNOWN";
        Pass(Rejects(missingConstructorParameter, "CONSTRUCTOR_PARAMETER_TYPES"),
            "unresolved constructor parameter type rejected");

        SelectedFactoryClosureManifest missingBaseConstructor = Clone(Valid());
        missingBaseConstructor.Nodes.Single(node => node.Kind == "BASE_CONSTRUCTOR").Classification = "UNSUPPORTED_REQUIRED";
        SelectedFactoryClosureResult blockedBaseConstructor = SelectedFactoryTypeClosure.Validate(missingBaseConstructor);
        Pass(blockedBaseConstructor is { FullyClosed: 0, Blocked: 1, Unknown: 0 } &&
             blockedBaseConstructor.Violations.Any(message => message.Contains("BASE_CONSTRUCTOR",
                 StringComparison.OrdinalIgnoreCase)), "unresolved base constructor rejected");

        SelectedFactoryClosureManifest unsupportedStaticConstructor = Clone(Valid());
        unsupportedStaticConstructor.Nodes.Single(node => node.Kind == "TYPE_INITIALIZER").Classification = "UNSUPPORTED_REQUIRED";
        Pass(Rejects(unsupportedStaticConstructor, "TYPE_INITIALIZER"), "unsupported static constructor rejected");

        SelectedFactoryClosureManifest unsupportedModuleHook = Clone(Valid());
        unsupportedModuleHook.Nodes.Single(node => node.Kind == "MODULE_LOAD").Classification = "UNSUPPORTED_REQUIRED";
        Pass(Rejects(unsupportedModuleHook, "MODULE_LOAD"), "unsupported selected module-load hook rejected");

        SelectedFactoryClosureManifest unresolvedReflection = Clone(Valid());
        unresolvedReflection.Nodes.Single(node => node.Kind == "REFLECTION").Classification = "UNSUPPORTED_REQUIRED";
        Pass(Rejects(unresolvedReflection, "REFLECTION"), "required reflection without lowering rejected");

        SelectedFactoryClosureManifest unresolvedIl = Clone(Valid());
        unresolvedIl.Nodes.Single(node => node.Kind == "HOOKS").Classification = "UNSUPPORTED_REQUIRED";
        Pass(Rejects(unresolvedIl, "HOOKS"), "required IL hook without frozen plan rejected");

        SelectedFactoryClosureManifest unresolvedMethodSignature = Clone(Valid());
        unresolvedMethodSignature.Nodes.Single(node => node.Kind == "LIFECYCLE").Dependencies =
            ["unresolved-method-signature-type"];
        Pass(Rejects(unresolvedMethodSignature, "unresolved-method-signature-type"),
            "method signature containing unresolved runtime type rejected");

        SelectedFactoryClosureManifest knownIdIncomplete = Clone(Valid());
        knownIdIncomplete.Factories[0].RootNodeIds = knownIdIncomplete.Factories[0].RootNodeIds
            .Where(id => id != "content").ToArray();
        Pass(Rejects(knownIdIncomplete, "missing accepted required closure dimension CONTENT"),
            "known factory ID with incomplete semantic closure rejected");

        return passed;
    }
}
