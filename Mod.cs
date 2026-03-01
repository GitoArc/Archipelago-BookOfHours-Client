using MelonLoader;
using Newtonsoft.Json;
using SecretHistories.Entities;
using SecretHistories.Spheres;
using SecretHistories.Tokens.Payloads;
using SecretHistories.UI;
using UnityEngine;
using UnityEngine.SceneManagement;


[assembly: MelonInfo(typeof(Mod), "AP_BookOfHours", "1.0.0", "Gito", null)]
[assembly: MelonGame("Weather Factory", "Book of Hours")]

public class Mod : MelonMod
{
    public const string PluginGUID = "com.yourName.projectName";
    public const string PluginName = "BepInEx5ArchipelagoPluginTemplate";
    public const string PluginVersion = "1.0.0";

    public const string ModDisplayInfo = $"{PluginName} v{PluginVersion}";
    private const string APDisplayInfo = $"Archipelago v{ArchipelagoClient.APVersion}";
    public static ArchipelagoClient ArchipelagoClient;

    public static Compendium Compendium { get; set; }
    public static HornedAxe HornedAxe { get; set; }

    /// <summary>
    /// Because the spawning of elements uses the Id, but I use Labels for AP stuff,
    /// I need a simple/fast/direct way of reverting the label.
    /// </summary>
    public static Dictionary<string, string> LabelToId { get; } = [];

    public static Sphere Sundries_Tab { get; private set; } = null;

    public override void OnInitializeMelon()
    {
        base.OnInitializeMelon();

        ArchipelagoClient = new ArchipelagoClient();

        MelonEvents.OnGUI.Subscribe(Draw);
        LoggerInstance.Msg("Initialized.");
    }

    public override void OnLateInitializeMelon()
    {
        base.OnLateInitializeMelon();

        //fetch unity-components
        Compendium = Watchman.Get<Compendium>();
        HornedAxe = Watchman.Get<HornedAxe>();

        // caching LabelToId
        foreach (var element in Compendium.GetEntitiesAsList<Element>())
        {
            if (element.IsAspect)
                continue;
            _ = LabelToId.TryAdd(element.Label, element.Id);
        }

        SceneManager.activeSceneChanged += (old, @new) =>
        {
            if (@new.name == "S4Library")
            {
                Sundries_Tab = GameObject.Find("MiscTraySphere").GetComponent<Sphere>();
            }
        };

    }
    public override void OnApplicationQuit()
    {
        base.OnApplicationQuit();
        SaveServerData();
    }

    private void SaveServerData()
    {
        File.WriteAllText(LocalFiles.SavefileWith(ArchipelagoClient.ServerData.Seed, "json"), ArchipelagoClient.ServerData.ToString());
    }

    private void Draw()
    {
        GUI.Label(new Rect(16, 16, 300, 20), ModDisplayInfo);
        ArchipelagoConsole.OnGUI();

        string statusMessage;
        // show the Archipelago Version and whether we're connected or not
        if (ArchipelagoClient.Authenticated)
        {
            // if your game doesn't usually show the cursor this line may be necessary
            // Cursor.visible = false;

            statusMessage = " Status: Connected";
            GUI.Label(new Rect(16, 50, 300, 20), APDisplayInfo + statusMessage);
            if (GUI.Button(new Rect(16, 130, 100, 20), "Disconnect"))
            {
                ArchipelagoClient.Disconnect();
            }
        }
        else
        {
            // if your game doesn't usually show the cursor this line may be necessary
            //Cursor.visible = true;

            statusMessage = " Status: Disconnected";
            GUI.Label(new Rect(16, 50, 300, 20), APDisplayInfo + statusMessage);
            GUI.Label(new Rect(16, 70, 150, 20), "Host: ");
            GUI.Label(new Rect(16, 90, 150, 20), "Player Name: ");
            GUI.Label(new Rect(16, 110, 150, 20), "Password: ");

            ArchipelagoClient.ServerData.Uri = GUI.TextField(new Rect(150, 70, 150, 20),
                ArchipelagoClient.ServerData.Uri);
            ArchipelagoClient.ServerData.SlotName = GUI.TextField(new Rect(150, 90, 150, 20),
                ArchipelagoClient.ServerData.SlotName);
            ArchipelagoClient.ServerData.Password = GUI.TextField(new Rect(150, 110, 150, 20),
                ArchipelagoClient.ServerData.Password);

            // requires that the player at least puts *something* in the slot name
            if (GUI.Button(new Rect(16, 130, 100, 20), "Connect") &&
                !string.IsNullOrWhiteSpace(ArchipelagoClient.ServerData.SlotName))
            {
                ArchipelagoClient.Connect();
            }
        }
        // this is a good place to create and add a bunch of debug buttons
    }

    public override void OnSceneWasLoaded(int buildIndex, string sceneName)
    {
        base.OnSceneWasLoaded(buildIndex, sceneName);

        if (sceneName == "S4Library")
        {
            if (!File.Exists(LocalFiles.Jsondump_PATH))
                PrepareAndDumpJsonTo(LocalFiles.Jsondump_PATH);
        }
    }

    private List<JsonLine> jsondump = [];
    void PrepareAndDumpJsonTo(string fullFilePathWithExtension)
    {
        /////////////////////////////////////////////////////////////////////////////////////
        var ar = UnityEngine.Object.FindObjectsOfType<ConnectedTerrain>();
        List<ConnectedTerrain> terrainTokens = [];
        List<WisdomNodeTerrain> wisdomtreeRAW = [];
        foreach (var terrain in ar)
        {
            if (terrain is WisdomNodeTerrain)
            {
                wisdomtreeRAW.Add(terrain as WisdomNodeTerrain);
            }
            else
            {
                terrainTokens.Add(terrain);
            }
        }
        var center = wisdomtreeRAW.First(a => a.Id == "wt.memorylocus");
        wisdomtreeRAW.Remove(center);
        wisdomtreeRAW = [.. wisdomtreeRAW.OrderBy(a => a.Id)];
        wisdomtreeRAW = [.. wisdomtreeRAW.Prepend(center)];

        //////////////////////////////////
        Recipe[] terrainsRAW = [.. Compendium.GetEntitiesMatchingWildcardId<Recipe>("terrain.*")
            .OrderBy(a => a.Id)];
        Dictionary<string, ConnectedTerrain[]> dic = [];
        foreach (var t in terrainTokens)
        {
            var conns = t.Patch_GetConnectedTerrain();//<- since these are alive gameobject datas, do not modify?
            // weird1: the Bridge has the Gatehouse in its ConnectedTerrains (the room AFTER unlocking the lodge), yet the bridge does NOT unlock the Gateouse in game
            // to not have such misinformation propagate to jsondump; remove from connections
            if (t.Id == "cucurbitbridge" && conns.Any(a => a.Id.Contains("gatehouse")))
            {
                conns = [.. conns.Where(a => !a.Id.Contains("gatehouse"))];
            }
            // terrainTokens are missing the 'terrains.' prefix that terrainsRAW uses; add it to the tokens so that we can match it with the terrainsRaw entries
            dic.Add($"terrain.{t.Id}", conns);
        }
        /// ////////////////////////////////////////////////////////////////////////////////
        Element[] booksRAW = [.. Compendium.GetEntitiesMatchingWildcardId<Element>("t.*")
            .OrderBy(a => a.Id)];
        /// ///////////////////////////////////////////////////////////////////////////
        ///health has a slightly different description, but this string matches all nine parts well enough: !! USE "X", NOT "X."
        Element[] soulsRAW = [.. Compendium.GetEntitiesMatchingWildcardId<Element>("x*").Where(a => a.Desc.Contains("of the nine parts of the")).ToList()
            .OrderBy(a => a.Id)];
        /// //////////////////////////////////////////////////////////////////////////////////
        var prec = Compendium.GetEntitiesMatchingWildcardId<Element>("precursor.*").ToList();
        var precursorToMemories = prec.Where(a => a.Aspects.ContainsKey("memory")).ToList();
        var weathers = Compendium.GetEntitiesMatchingWildcardId<Element>("weather.*").ToList();
        var memoriesRAW = new List<Element>();
        foreach (var e in precursorToMemories)
        {
            var mem = Compendium.GetEntityById<Element>(e.DecayTo);
            memoriesRAW.Add(mem);
        }
        memoriesRAW.AddRange(weathers);
        memoriesRAW = [.. memoriesRAW.OrderBy(a => a.Id)];

        Element[] lessonsRAW = [.. Compendium.GetEntitiesMatchingWildcardId<Element>("x.*")
            .OrderBy(a=>a.Id)];
        //the lesson 'x.summon.echidna' has no equivalent skill - I'll manually put it at the end of the list, only so that everyone else's id match better :>
        List<Element> lessonsModif = [.. lessonsRAW];
        var echidna = lessonsModif.First(a => a.Id == "x.summon.echidna");
        lessonsModif.Remove(echidna);
        lessonsModif.Add(echidna);
        lessonsRAW = [.. lessonsModif];
        ///var lessonsByLabel = c.GetEntitiesAsList<Element>().Where(a => a.Label.Contains("Lesson:")).ToList(); MATCHES lessons.count==74
        /// ////////////////////////////////////
        Element[] skillsRAW = [.. Compendium.GetEntitiesMatchingWildcardId<Element>("s.*")
            .OrderBy(a => a.Id)];
        /// ////////////////////////////////////
        AddToJsondump(Make(memoriesRAW, "10"));
        AddToJsondump(Make(soulsRAW, "20"));
        AddToJsondump(Make(terrainsRAW, dic, "30"));
        AddToJsondump(Make(wisdomtreeRAW, "40"));
        AddToJsondump(MakeBooks(booksRAW, "50"));
        AddToJsondump(Make(lessonsRAW, "60"));
        AddToJsondump(Make(skillsRAW, "70"));

        string LocalLowPath =
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData).Replace("Roaming", "LocalLow");
        var modPath = Path.Combine(LocalLowPath, "Weather Factory", "Book of Hours", "mods");
        var fPath = Path.Combine(modPath, "dump.json");
        var jsonStr = JsonConvert.SerializeObject(jsondump, Formatting.Indented);
        File.WriteAllText(fullFilePathWithExtension, jsonStr);
    }

    private void AddToJsondump(IEnumerable<JsonLine> arr)
    {
        foreach (JsonLine e in arr)
        {
            jsondump.Add(e);
        }
    }
    private void AddToJsondump(IEnumerable<TerrainJsonLine> arr)
    {
        foreach (TerrainJsonLine e in arr)
        {
            jsondump.Add(e);
        }
    }

    private ElementJsonLine[] Make(IEnumerable<Element> ar, string startID)
    {
        List<ElementJsonLine> res = [];
        int i = 0;
        foreach (var e in ar)
        {
            res.Add(new ElementJsonLine { IdStr = e.Id, Label = e.Label, Aspects = e.Aspects, ApId = int.Parse(startID + $"{i + 1}") });
            i++;
        }
        return [.. res];
    }
    private BookJsonLine[] MakeBooks(IEnumerable<Element> ar, string startID)
    {
        List<BookJsonLine> res = [];
        int i = 0;
        foreach (var e in ar)
        {
            res.Add(new BookJsonLine
            {
                IdStr = e.Id,
                Label = e.Label,
                Aspects = e.Aspects,
                ApId = int.Parse(startID + $"{i + 1}"),
                Rewards = e.XTriggers.ToDictionary(a => a.Key, a => a.Value.Single().Id)
            });
            i++;
        }
        return [.. res];
    }
    private TerrainJsonLine[] Make(IEnumerable<Recipe> ar, Dictionary<string, ConnectedTerrain[]> dic, string startID)
    {
        List<TerrainJsonLine> res = [];
        int i = 0;
        foreach (Recipe e in ar)
        {
            ConnectedTerrain[] cons = dic.TryGetValue(e.Id, out cons) ? cons : [];
            TerrainSimpleDetails[] t = [.. cons.Select(a => new TerrainSimpleDetails { IdStr = $"terrain.{a.Id}", Preface = a.Preface, Label = a.Label })];
            var d = e.PreSlots[0].Required;
            res.Add(new TerrainJsonLine
            {
                IdStr = e.Id,
                Preface = e.Preface,
                Label = e.Label,
                ApId = int.Parse(startID + $"{i + 1}"),
                Requires = d,
                ConnectsTo = t
            });
            i++;
        }
        return [.. res];
    }
    private JsonLine[] Make(IEnumerable<WisdomNodeTerrain> ar, string startID)
    {
        List<JsonLine> res = [];
        int i = 0;
        foreach (var e in ar)
        {
            res.Add(new JsonLine { IdStr = e.Id, Label = MakeLabelFromWisdomIdstr(e.Id), ApId = int.Parse(startID + $"{i + 1}") });
            i++;
        }
        return [.. res];
    }

    public static string MakeLabelFromWisdomIdstr(string idStr)
    {
        string labl;
        if (idStr == "wt.memorylocus")
        {
            labl = "The Roots of Memory";
        }
        else
        {
            var split = idStr.Replace("wt.", "").Split('.');
            string pathRAW = split[0];
            string level = split[1];
            string path = pathRAW switch
            {
                "bir" => "Birdsong",
                "bos" => "TheBosk",
                "hor" => "Horomachistry",
                "hus" => "Hushery",
                "ill" => "Illumination",
                "ith" => "Ithastry",
                "nyc" => "Nyctodromy",
                "pre" => "Perservation",
                "sko" => "Skolekosophy",
                _ => throw new ArgumentException(idStr),
            };
            //convert to roman
            level = level switch
            {
                "1" => "I",
                "2" => "II",
                "3" => "III",
                "4" => "IV",
                "5" => "V",
                "6" => "VI",
                "7" => "VII",
                "8" => "VIII",
                "9" => "IX",
                _ => throw new ArgumentException(idStr),
            };
            labl = $"{path} {level}";
        }

        return labl;
    }
}


public class JsonLine
{
    [JsonProperty(Order = 1)] public string IdStr { get; set; }

    [JsonProperty(Order = 5)] public string Label { get; set; }

    [JsonProperty(Order = 10)] public long ApId { get; set; }

    //as method so it wont get serialized
    public string GetCategory()
    {
        string sap = $"{ApId}";
        var splits = sap.Split('0', 2);
        var category = splits[0];
        return category switch
        {
            "1" => "memory",
            "2" => "soul",
            "3" => "terrain",
            "4" => "wisdom",
            "5" => "book",
            "6" => "lesson",
            "7" => "skill",
            "8" => "craft",
            _ => throw new NotImplementedException(),
        };
    }
}
public class ElementJsonLine : JsonLine
{
    [JsonProperty(Order = 6)] public Dictionary<string, int> Aspects { get; set; }
}
public class TerrainJsonLine : JsonLine
{
    [JsonProperty(Order = 4)] public string Preface { get; set; }
    [JsonProperty(Order = 6)] public Dictionary<string, int> Requires { get; set; }

    [JsonProperty(Order = 15)]
    public TerrainSimpleDetails[] ConnectsTo { get; set; }
}

public class TerrainSimpleDetails
{
    [JsonProperty(Order = 1)] public string IdStr { get; set; }
    [JsonProperty(Order = 2)] public string Preface { get; set; }
    [JsonProperty(Order = 3)] public string Label { get; set; }
}

public class BookJsonLine : ElementJsonLine
{
    [JsonProperty(Order = 5)] public Dictionary<string, string> Rewards { get; set; }
}