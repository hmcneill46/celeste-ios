namespace AppleEverestBuilder;

// Observes the real dispatch sites without inserting synthetic lifecycle
// calls, changing tracker types, or adding a HookGen/IL compatibility class.
internal static class FactoryCanaryInstrumentation
{
    internal static void Apply(string root)
    {
        const string observer = "global::Celeste.Mod.AppleEverestFactoryCanary.";
        void Replace(string file, string before, string after, int expected = 1)
        {
            string path = Path.Combine(root, file);
            string source = File.ReadAllText(path);
            if (source.Split(before, StringSplitOptions.None).Length - 1 != expected)
                throw new InvalidDataException("factory lifecycle dispatch shape changed: " + file + ":" + before.Trim());
            File.WriteAllText(path, source.Replace(before, after, StringComparison.Ordinal));
        }
        void Dispatch(string file, string indent, string target, string stage, string arguments, int count = 1)
        {
            string call = indent + target + "." + stage + "(" + arguments + ");";
            Replace(file, call,
                indent + observer + "Dispatch(" + target + ", \"" + stage + "\", false);\n" + call + "\n" +
                indent + observer + "Dispatch(" + target + ", \"" + stage + "\", true);", count);
        }
        const string entities = "Monocle/EntityList.cs";
        Dispatch(entities, "\t\t\t\t\t\t", "entity", "Added", "Scene");
        Dispatch(entities, "\t\t\t\t\t\t", "entity2", "Removed", "Scene");
        Dispatch(entities, "\t\t\t\t", "item", "Awake", "Scene");
        Dispatch(entities, "\t\t\t\t", "entity", "Update", "");
        Dispatch(entities, "\t\t\t\t", "entity", "Render", "", 4);
        Dispatch("Monocle/Scene.cs", "\t\t\t", "entity", "SceneEnd", "this");
        Replace("Monocle/Entity.cs", "\tpublic void RemoveSelf()\n\t{",
            "\tpublic void RemoveSelf()\n\t{\n\t\t" + observer + "RemovalRequested(this);");
        foreach (string stage in new[] { "BeforeRender", "Update", "Ended" })
            Dispatch("Celeste/BackdropRenderer.cs", "\t\t\t", "backdrop", stage, "scene");
        Dispatch("Celeste/BackdropRenderer.cs", "\t\t\t\t", "backdrop", "Render", "scene");
        Replace("Celeste/MapData.cs", "\t\treturn backdrop;",
            "\t\t" + observer + "Dispatch(backdrop, \"Configured\", true);\n\t\treturn backdrop;");
        foreach ((string name, string layer) in new[] { ("backdrop", "Background"), ("backdrop2", "Foreground") })
        {
            string assignment = "\t\t\t" + name + ".Renderer = Level." + layer + ";";
            Replace("Celeste/LevelLoader.cs", assignment, assignment + "\n\t\t\t" + observer + "Dispatch(" + name + ", \"Attached\", true);");
        }
        Replace("Celeste/Level.cs", "\t\tCamera.Position = cameraPreShake;",
            "\t\tCamera.Position = cameraPreShake;\n\t\t" + observer + "Frame(this);");
    }
}
