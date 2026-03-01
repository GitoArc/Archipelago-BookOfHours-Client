using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.BounceFeatures.DeathLink;
using Archipelago.MultiClient.Net.Enums;
using Archipelago.MultiClient.Net.Helpers;
using Archipelago.MultiClient.Net.Models;
using Archipelago.MultiClient.Net.Packets;
using MelonLoader;
using Newtonsoft.Json;
using SecretHistories.Entities;
using SecretHistories.UI;
using UnityEngine.SceneManagement;


public class ArchipelagoClient
{
    public const string APVersion = "0.5.0";
    private const string Game = "Book of Hours";

    public static bool Authenticated { get; private set; }
    private bool AttemptingConnection { get; set; }

    public static ArchipelagoData ServerData { get; private set; } = new(); // if null, the GUI in Mod.cs will keep throwing
    private DeathLinkHandler DeathLinkHandler { get; set; }
    private ArchipelagoSession Session { get; set; } = null;

    private event Action<LoginSuccessful> LoginSuccess = null;

    /// <summary>
    /// ""Items"" that have no own location and must be injected into "local" rewards (NOT send to AP).
    /// MUST be an IdStr in format 'lowercase.xyz.etc'
    /// </summary>
    List<string> EventItems { get; set; } = [];

    // use a Dict<string, ItemList> to categorise the received Items from Socket?
    // would allow for dumping all filler items in one recipe, but prioritise single-rewarding prog/useful
    // but the index is in order of "found": What if 1-99 are filler and then a prog is found?
    //  once I skip the index ahead, the normal behaviour will ignore the smaller indexes

    public HashSet<string> LocalLocationsReached_NotYetSent_LABELS { get; private set; } = [];
    public List<ItemInfo> NotYetRewardedItems { get; private set; } = [];

    private Dictionary<string, long> LOCATIONS { get; set; }
    private Dictionary<string, long> ITEMS { get; set; }

    private Random Random { get; set; } = new Random(0);
    private Dictionary<string, long> slotData_MemoryProgression { get; set; } = null;


    public ArchipelagoClient()
    {
        LoginSuccess += OnLoginSuccess;

        //static events; only subscrib ONCE (caused memory leak in SetupSession with repeated subscriptions)
        Patch_TransitionToState.StateIsRequiExec_Prefix += HandleApLoop;
        Patch_TransitionToState.StateIsRequiExec_Postfix += OnCompletedRecipe_Postfix;
        Patch_TransitionToState.StateIsRequiExec_Postfix += CheckVictory;

        Patch_WisdomNodeTerrain.WisdomNodeCommitted += OnWisdomTreeNodeCommitted;
        Patch_WisdomNodeTerrain.OutputTokenSpawned += OnWisdomTreeOutput;
    }

    private void OnWisdomTreeOutput(string id)
    {
        //check soul locations
    }

    private void OnWisdomTreeNodeCommitted(string id)
    {   //expected format: wt.PATH.LEVEL_ARABIC

        //log accumulative progression
        ServerData.Progression_Wisdoms_TotalCommitted++;
        for (int i = 1; i <= ServerData.Progression_Wisdoms_TotalCommitted; i++)
        {
            string str = i == 1 ? $"Committed 1 skill" : $"Committed {i} skills";
            _ = LocalLocationsReached_NotYetSent_LABELS.Add(str);
        }
        //log tier progression
        var splits = id.Replace("wt.", "").Split('.');
        int tier = 0;
        if (splits[0] != "memorylocus")
        {
            tier = int.Parse(splits[1]);
        }
        for (int i = 1; i <= tier; i++)
        { //this does not add a "Tier 0" for the Journal
            _ = LocalLocationsReached_NotYetSent_LABELS.Add($"Reached Tier {i}");
        }
        //log specific location
        _ = LocalLocationsReached_NotYetSent_LABELS.Add($"Committed {Mod.MakeLabelFromWisdomIdstr(id)}");
    }

    private KeyValuePair<string, long>[] GetNotYetCheckedLocations()
    {
        if (Session is null)
            return [];

        var server = Session.Locations.AllMissingLocations;

        var l2 = (from id in Session.Locations.AllMissingLocations
                  join l in ServerData.LOCATIONS on id equals l.Value
                  select l).ToArray();
        return l2;
    }

    private void OnLoginSuccess(LoginSuccessful success)
    {   /// the data in 'this.Session' was asserted valid

        Authenticated = true;
        //request the location_to_id and item_to_id mappings
        Session.Socket.SendPacket(new GetDataPackagePacket() { Games = ["Book of Hours"] });

        DeathLinkHandler = new(Session.CreateDeathLinkService(), ServerData.SlotName);

        ServerData = TryLoadPreviousServerDataElseCreateNew(success, Session);
        // should've applied via savedata, but just in case, sync with replied data
        ServerData.CheckedLocations.UnionWith(Session.Locations.AllLocationsChecked);

        ulong lo = ulong.Parse(ServerData.Seed);
        int i = (int)(int.MaxValue & lo);
        Random = new Random(i); //maybe Random(0) is enough? Since it heavily depends what actions the user does, this might be indistinguishable from seeded random?

        slotData_MemoryProgression = JsonConvert.DeserializeObject<Dictionary<string, long>>(ServerData.SlotData["memory_progression"].ToString());

        ArchipelagoConsole.LogMessage($"Successfully connected to {ServerData.Uri} as {ServerData.SlotName}!");
    }

    private ArchipelagoData TryLoadPreviousServerDataElseCreateNew(LoginSuccessful success, ArchipelagoSession session)
    {
        //theoretically I only need Session to set slotData via session.DataStorage.GetSlotData()...
        //  but go and require LoginSuccessful to verify that the Session is indeed valid

        ArchipelagoData data;

        string fname = LocalFiles.SavefileWith(session.RoomState.Seed, "json");
        if (File.Exists(fname))
        {
            data = JsonConvert.DeserializeObject<ArchipelagoData>(File.ReadAllText(fname));
        }
        else
        {
            data = new ArchipelagoData();
            data.SetupSession(success.SlotData, session.RoomState.Seed);
        }

        return data;
    }

    /// <summary>
    /// call to connect to an Archipelago session. Connection info should already be set up on ServerData
    /// </summary>
    /// <returns></returns>
    public void Connect()
    {
        if (Authenticated || AttemptingConnection) return;

        try
        {
            var session = ArchipelagoSessionFactory.CreateSession(ServerData.Uri);
            Session = SetupSession(session);
        }
        catch (Exception e)
        {
            Melon<Mod>.Logger.Error(e);
            return;
        }

        TryConnect();
    }

    /// <summary>
    /// add handlers for Archipelago events
    /// </summary>
    private ArchipelagoSession SetupSession(ArchipelagoSession session)
    {
        session.MessageLog.OnMessageReceived += message => ArchipelagoConsole.LogMessage(message.ToString());

        session.Items.ItemReceived += OnItemReceived;
        session.Socket.PacketReceived += OnPacketReceived;
        session.Socket.ErrorReceived += OnSessionErrorReceived;
        session.Socket.SocketClosed += OnSessionSocketClosed;

        var typs = Mod.Compendium.GetEntityTypes().ToArray();
        var verbs1 = Mod.Compendium.GetEntitiesAsAlphabetisedList<Verb>();
        var vbs2 = Mod.Compendium.GetEntitiesAsList<Verb>();

        return session;
    }

    private void CheckVictory(TransitionToStateEventArgs e)
    {
        if (SceneManager.GetActiveScene().name != "S4Library")
            return;

        //if (e.Situation.CurrentRecipeId != "archipelago.recipe.reward")
        //    return;   THIS way, every recipe will cause this check to run...which is good? too spam-y?


        int count = Mod.Sundries_Tab.Tokens.Count(a => a.PayloadEntityId == "archipelago.goal.part");
        if (count >= Convert.ToInt32(ServerData.SlotData["victory_shards"]))
        {
            Session.SetClientState(ArchipelagoClientState.ClientGoal);
        }
    }

    private void OnPacketReceived(ArchipelagoPacketBase packet)
    {
        if (packet is DataPackagePacket d)
        {
            ServerData.LOCATIONS = d.DataPackage.Games["Book of Hours"].LocationLookup;
            ServerData.ITEMS = d.DataPackage.Games["Book of Hours"].ItemLookup;
        }
    }

    private void HandleApLoop(TransitionToStateEventArgs e)
    {
        //handle ONLY the offstage-situation that rewards ap-items
        if (e.Situation.CurrentRecipeId != "archipelago.recipe.reward")
            return;

        var rec = e.Situation.GetCurrentRecipe();
        rec.Notable = false;
        rec.Effects.Clear();
        if (EventItems.Count > 0)
        {
            rec.Effects.TryAdd(EventItems[0], "1");
        }
        EventItems.RemoveAt(0);
        // Multi-Handling all items (but no exact descriptor)
        //foreach (var pending in EventItems)
        //{
        //    if (!rec.Effects.TryAdd(pending, "1"))
        //    {
        //        rec.Effects[pending] = $"{int.Parse(rec.Effects[pending]) + 1}";
        //    }
        //}
        //EventItems.Clear();

        //prioritises
        if (rec.Effects.Count is 0
            && NotYetRewardedItems.Count > 0)
        {
            ///single reward lets me customize the log better
            //foreach (var item in NotYetRewardedItems) {
            //    var n = item.ItemName.ToLower();
            //    if (!ree.Effects.TryAdd(n, "1")) {
            //        int i = int.Parse(ree.Effects[n]);
            //        ree.Effects[n] = $"{++i}";
            //    }
            //}

            //but after a !release with like 20+ Items, it feels too spam-y

            var item = NotYetRewardedItems[0];
            NotYetRewardedItems.RemoveAt(0);
            //handle "event"items
            if (item.ItemName == null)
            {

            }
            rec.Effects.Add(Mod.LabelToId[item.ItemName], "1");
            rec.Desc = $"{item.Player.Name} has sent you {item.ItemName}.\n\n There are {NotYetRewardedItems.Count} more items in queue...";
            rec.Notable = true;
        }
    }

    private void OnCompletedRecipe_Postfix(TransitionToStateEventArgs e)
    {
        if (e.Situation.CurrentRecipeId == "NullRecipe"
            || e.Situation.CurrentRecipeId.Contains("day.")
            || e.Situation.CurrentRecipeId == "visitorpatrol"
            || e.Situation.CurrentRecipeId == "archipelago.recipe.loop")
        {
            return;
        }

        //study.mystery.lantern.mastering.yes

        var output = e.Situation.GetSingleSphereByCategory(SecretHistories.Enums.SphereCategory.Output);
        if (output.Id == "Nullsphere")
            return;

        var rec = e.Situation.GetCurrentRecipe();

        List<string> _happened_locations_LABELS = [];
        List<Element> all_elements_in_output = [];
        HashSet<long> missing_locations = [.. Session.Locations.AllMissingLocations];

        if (e.Situation.CurrentRecipeId.Contains("terrain."))
        {
            _happened_locations_LABELS.Add(e.Situation.GetCurrentRecipe().Label);
        }
        else if (false) //x. s. wisdom craft
        {
        }
        else // memory probably? or Considered stuff like packages? Literally anything that uses OutputSphere?
        {
            foreach (Token t in output.Tokens)
            {
                ElementStack _payload = (ElementStack)t.Payload;
                // ! DecayTo is an IdStr ! for consistency, dont use Element.Label
                string idstr = _payload.Element.Decays ? _payload.Element.DecayTo : _payload.Element.Id;
                if (idstr == "")    //happens when assistance unlocks smth (or introduces to other villagers) and turns into 'Departure': It decays to "", and 'GetEntityById' returns a "NULL_ELEMENT_ID"
                    continue;         //...which in turn causes LOCATIONS.Where(elem.Label) to return EVERYTHING
                                      // safest bet is to ignore this token?

                var elem = Mod.Compendium.GetEntityById<Element>(idstr);
                all_elements_in_output.Add(elem);
            }
            _happened_locations_LABELS.AddRange(all_elements_in_output.Select(a => a.Label));
        }
        ////////////////////////////////////////////
        //add progressive locations
        if (all_elements_in_output.Count > 0)
        {
            //////////////////////////////////////////
            //roll chance if location does count for progression
            foreach (var elem in all_elements_in_output)
            {
                if (elem.Inherits.Contains("memory"))
                {
                    //if (e.Situation.CurrentRecipeId == "archipelago.recipe.reward"
                    //    && slotData_MemoryProgression["allow_ap_item_to_proc"] == 0)
                    //    continue;

                    int roll = Random.Next(1, 101);
                    if (roll <= GetChanceBasedOnCategory(elem))
                    {
                        ServerData.Progression_Memories_TotalRemembered++;
                    }
                }
            }


            for (int i = 1; i <= ServerData.Progression_Memories_TotalRemembered; i++)
            {
                var kes = ServerData.LOCATIONS.Select(a => a.Key).ToHashSet();
                var pred = kes.Where(a => a.Contains($"Remember {i} "));
                foreach (var p in pred)
                    _happened_locations_LABELS.Add(p);
            }

            if (ServerData.Progression_Memories_TotalRemembered == Convert.ToInt32(slotData_MemoryProgression["goal"]))
            {
                EventItems.Add("archipelago.goal.part");
            }
        }

        //send to server
        if (_happened_locations_LABELS.Count > 0 && missing_locations.Count > 0)
        {
            HashSet<KeyValuePair<string, long>> alloweds = [.. LOCATIONS.Where(a => missing_locations.Contains(a.Value))];
            KeyValuePair<string, long>[] pairs = [.. alloweds.Where(a => _happened_locations_LABELS.Any(b => a.Key.Contains(b)))];
            if (pairs.Length > 0)
            {
                long[] unchecked_locations = [.. pairs.Select(a => a.Value)];

                Session.Locations.CompleteLocationChecks(unchecked_locations);
            }
        }

        return;
    }

    private int GetChanceBasedOnCategory(Element elem)
    {
        long percent;
        if (elem.Id.Contains("mem."))
            percent = slotData_MemoryProgression["basics_chance"];
        else if (elem.Aspects.ContainsKey("sound"))
            percent = slotData_MemoryProgression["musics_chance"];
        else if (elem.Aspects.ContainsKey("weather"))
        {
            percent = slotData_MemoryProgression["weathers_chance"];
            if (elem.Id == "weather.earthquake")
                percent = slotData_MemoryProgression["weather_earthquake_chance"];
            else if (elem.Id == "weather.numa")
                percent = slotData_MemoryProgression["weather_numa_chance"];
        }
        else if (elem.Aspects.ContainsKey("lesson"))
            percent = slotData_MemoryProgression["lessons_chance"];
        else if (elem.Aspects.ContainsKey("persistent"))
            percent = slotData_MemoryProgression["persistents_chance"];
        else
            percent = slotData_MemoryProgression["leftovers_chance"];

        return (int)percent;
    }

    /// <summary>
    /// attempt to connect to the server with our connection info
    /// </summary>
    private void TryConnect()
    {
        AttemptingConnection = true;

        try
        {
            HandleConnectResult(
                 Session.TryConnectAndLogin(
                     Game,
                     ServerData.SlotName,
                     ItemsHandlingFlags.IncludeOwnItems,
                     new Version(APVersion),
                     password: ServerData.Password,
                     requestSlotData: ServerData.NeedSlotData
                 ));
        }
        catch (Exception e)
        {
            Melon<Mod>.Logger.Error(e);
            HandleConnectResult(new LoginFailure(e.ToString()));
        }

        AttemptingConnection = false;
    }

    /// <summary>
    /// handle the connection result and do things
    /// </summary>
    /// <param name="result"></param>
    private void HandleConnectResult(LoginResult result)
    {
        if (result.Successful)
        {
            LoginSuccess?.Invoke((LoginSuccessful)result);
        }
        else
        {
            var outText = $"Failed to connect to {ServerData.Uri} as {ServerData.SlotName}.";
            outText = ((LoginFailure)result).Errors.Aggregate(outText, (current, error) => current + $"\n    {error}");

            Melon<Mod>.Logger.Error(outText);

            Disconnect();
        }
    }

    /// <summary>
    /// something went wrong, or we need to properly disconnect from the server. cleanup and re null our session
    /// </summary>
    public void Disconnect()
    {
        Melon<Mod>.Logger.Msg("Disconnecting...");
        Session?.Socket.DisconnectAsync();

        Session = null;
        AttemptingConnection = false;
        Authenticated = false;
    }

    public void SendMessage(string message)
    {
        Session.Socket.SendPacketAsync(new SayPacket { Text = message });
    }

    private void OnItemReceived(ReceivedItemsHelper helper)
    {
        var receivedItem = helper.DequeueItem();

        if (helper.Index <= ServerData.Latest_Index_from_Server) return;

        //https://github.com/ArchipelagoMW/Archipelago/blob/main/docs/network%20protocol.md#synchronizing-items
        if (helper.Index == 0)
        {
            //abandon previous inventory // purge?
        }
        ServerData.Latest_Index_from_Server = helper.Index;


        // TODO reward the item here
        // if items can be received while in an invalid state for actually handling them, they can be placed in a local
        // queue/collection to be handled later

        NotYetRewardedItems.Add(receivedItem);
    }

    /// <summary>
    /// something went wrong with our socket connection
    /// </summary>
    /// <param name="e">thrown exception from our socket</param>
    /// <param name="message">message received from the server</param>
    private void OnSessionErrorReceived(Exception e, string message)
    {
        Melon<Mod>.Logger.Error(e);
        ArchipelagoConsole.LogMessage(message);
    }

    /// <summary>
    /// something went wrong closing our connection. disconnect and clean up
    /// </summary>
    /// <param name="reason"></param>
    private void OnSessionSocketClosed(string reason)
    {
        Melon<Mod>.Logger.Error($"Connection to Archipelago lost: {reason}");
        Disconnect();
    }
}