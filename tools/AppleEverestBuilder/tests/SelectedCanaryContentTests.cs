using AppleEverestBuilder;

internal static class SelectedCanaryContentTests
{
    internal static int Run(string temporary)
    {
        string root = Path.Combine(temporary, "typed-canary-content");
        Directory.CreateDirectory(root);
        string source = Path.Combine(root, "source.xml");
        string output = Path.Combine(root, "compiled");
        Directory.CreateDirectory(output);
        string map = "<Map><levels><level name='lvl_fixture' x='-424' y='-640' width='320' height='184'>" +
            "<entities><player id='1' x='24' y='168'/><appleEverestEntity name='CherryHelper/AssistRect' id='2' x='64' y='64' width='32' height='24' color='000000' appleEverestStrings='color'/></entities>" +
            "<triggers/><solids/><bg/></level></levels><Filler/><Style><Backgrounds>" +
            "<appleEverestBackdrop name='FlaglinesAndSuch/customGodrays' color='446666' appleEverestStrings='color'/></Backgrounds><Foregrounds/></Style></Map>";
        File.WriteAllText(source, map);
        string logical = ContentCompiler.Stage(source, "Content/Maps/Fixture/Typed.xml", output);
        string compiled = Path.Combine(output, logical);
        var elements = ContentCompiler.InspectElements(compiled);
        var progression = ContentCompiler.InspectProgression(compiled, logical, Hashing.FileSha256(compiled));
        if (!progression.Rooms.SequenceEqual(new[] { "fixture" }))
            throw new Exception("registered canary room validation must agree with actual LevelData legacy-prefix normalization");
        var entity = elements.Single(element => element.Id == "CherryHelper/AssistRect");
        if (entity.Attributes["color"] != "000000" || entity.Attributes.ContainsKey("appleEverestStrings") ||
            entity.RoomX != -424f || entity.RoomY != -640f)
            throw new Exception("typed canary must preserve string values and room origins without emitting compiler metadata");
        var backdrop = elements.Single(element => element.Id == "FlaglinesAndSuch/customGodrays");
        if (backdrop.Kind != "backdrop" || backdrop.Attributes["color"] != "446666" || backdrop.Attributes.ContainsKey("name"))
            throw new Exception("namespaced backdrop wrapper must compile to the exact runtime ID and attribute types");
        foreach (string bad in new[] { map.Replace("appleEverestStrings='color'", "appleEverestStrings='absent'"),
            map.Replace("<Backgrounds>", "<entities>").Replace("</Backgrounds>", "</entities>") })
        {
            File.WriteAllText(source, bad);
            bool rejected = false;
            try { _ = ContentCompiler.Stage(source, "Content/Maps/Fixture/Invalid.xml", output); }
            catch (InvalidDataException) { rejected = true; }
            if (!rejected) throw new Exception("invalid authored factory compiler metadata was accepted");
        }
        return 5;
    }
}
