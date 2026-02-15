using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.BounceFeatures.DeathLink;
using Archipelago.MultiClient.Net.Enums;
using Archipelago.MultiClient.Net.Helpers;
using Archipelago.MultiClient.Net.Models;
using Archipelago.MultiClient.Net.Packets;
using JetBrains.Annotations;
using MelonLoader;
using SecretHistories.Entities;
using SecretHistories.UI;
using UIWidgets.Extensions;

public class ArchipelagoClient
{
    public const string APVersion = "0.5.0";
    private const string Game = "Book of Hours";

    public static bool Authenticated;
    private bool attemptingConnection;

    public static ArchipelagoData ServerData = new();
    private DeathLinkHandler DeathLinkHandler;
    private ArchipelagoSession session;

    /// <summary>
    /// call to connect to an Archipelago session. Connection info should already be set up on ServerData
    /// </summary>
    /// <returns></returns>
    public void Connect()
    {
        if (Authenticated || attemptingConnection) return;

        try
        {
            session = ArchipelagoSessionFactory.CreateSession(ServerData.Uri);
            SetupSession();
        }
        catch (Exception e)
        {
            Melon<Mod>.Logger.Error(e);
        }

        TryConnect();
    }

    /// <summary>
    /// add handlers for Archipelago events
    /// </summary>
    private void SetupSession()
    {
        session.MessageLog.OnMessageReceived += message => ArchipelagoConsole.LogMessage(message.ToString());

        session.Items.ItemReceived += OnItemReceived;
        session.Socket.ErrorReceived += OnSessionErrorReceived;
        session.Socket.SocketClosed += OnSessionSocketClosed;
        //client events
        Patch_TransitionToState.TransitionToState_Prefix += OnPrefix;
        Patch_TransitionToState.TransitionToState_Postfix += CleanUpApRecipe;

        //var linkd = new LinkedRecipeDetails();
        //linkd.SetId("reward_ap_item_LinkedRec");
        //linkd.Additional = true;
        //linkd.Ambits = [];
        //linkd.Challenges = [];
        //linkd.Chance = 100;
        //linkd.OutputPath = "";
        //linkd.Shuffle = false;
        //linkd.ToPath = "~";
        //sit.SetCurrentRecipe(rec);

        var comp = Watchman.Get<Compendium>();
        var haxe = Watchman.Get<HornedAxe>();

        var typs = comp.GetEntityTypes().ToArray();
        var verbs1 = comp.GetEntitiesAsAlphabetisedList<Verb>();
        var vbs2 = comp.GetEntitiesAsList<Verb>();
        //Watchman.Register(verb);
        //comp.TryAddEntity(verb);

        //find day.daybreak
        var vv = haxe.GetSituationsWithVerbOfActionId("archipelago.receive.item");
        var c = comp.GetValidRecipesForUnstartedVerb("time").ToArray();
        var l = comp.GetEntitiesMatchingWildcardId<Recipe>("day.daybreak");
        var lAll = comp.GetEntitiesMatchingWildcardId<Recipe>("*");
        var lalt = lAll.Where(a=>a.Alt.Count > 0).ToList();
        var llink = lAll.Where(a => a.Linked.Count > 0).ToList();
        ///var l2 = compendium.GetEntitiesMatchingWildcardId<IEntityWithId>("day"); KEY NOT FOUND ERROR
        var ll = comp.GetEntitiesAsList<Recipe>().Select(a => a.Id == "day.daybreak").ToArray();
        ///var ll2 = compendium.GetEntitiesAsList<IEntityWithId>(); KEY NOT FOUND ERROR

        //var link = LinkedRecipeDetails.AsCurrentRecipe(ap_reward_recipe);
        //link.Chance = 100;
        //link.Additional = true;
        //link.Ambits = [];
        //link.Challenges = [];
        //link.ToPath = "~/incidents.offstage";
        //link.Expulsion = new Expulsion();
        //link.Expulsion.SetId("Expulsion");
        //link.Expulsion.Limit = 0;
        //link.Expulsion.ToPath = "";
        //link.Expulsion.Filter = [];
        //link.Expulsion.Lever = "";

        //l[0].Linked.Add(link);
        var x = 0;
    }

    private void CleanUpApRecipe(RecipeSuccessEventArgs arg)
    {
        if (arg.Situation.CurrentRecipeId != "ap.recipe.reward") return;

        var r = arg.Situation.GetCurrentRecipe();
        r.Effects.Clear();
    }

    long locTrack = 20;

    public List<ItemInfo> NotYetRewardedItems { get; private set; } = [];

    public Dictionary<string, string> LabelToId { get; set; } = [];
    private void OnPrefix(RecipeSuccessEventArgs e)
    {
        if (e.Situation.CurrentRecipeId == "ap.recipe.reward"
            && NotYetRewardedItems.Count > 0) {
            var re = e.Situation.GetCurrentRecipe();
            re.Effects = [];
            ///single reward lets me customize the log better
            //foreach (var item in NotYetRewardedItems) {
            //    var n = item.ItemName.ToLower();
            //    if (!ree.Effects.TryAdd(n, "1")) {
            //        int i = int.Parse(ree.Effects[n]);
            //        ree.Effects[n] = $"{++i}";
            //    }
            //}

            var item = NotYetRewardedItems[0];
            ///translate labelName to idName
            var c = Watchman.Get<Compendium>();
            var l = c.GetEntitiesAsList<Element>();
            foreach (var element in l)
            {
                if (element.IsAspect)
                    continue;
                LabelToId.TryAdd(element.Label, element.Id);
            }

            re.Effects.Add(LabelToId[item.ItemName], "1");
            re.Desc = $"{item.Player.Name} has sent you \n{item.ItemName}";
            re.Notable = true;
            NotYetRewardedItems.RemoveAt(0);
        }
        Melon<Mod>.Logger.Msg($"{e.Situation.CurrentRecipeId} has Completed, sending location {locTrack}");

        List<long> range = [];
        for (int i = 0; i <= locTrack; i++)
        {
            range.Add(i);
        }
        locTrack++;

        session?.Locations.CompleteLocationChecks([.. range]);

        var comp = Watchman.Get<Compendium>();
        var haxe = Watchman.Get<HornedAxe>();
    }

    /// <summary>
    /// attempt to connect to the server with our connection info
    /// </summary>
    private void TryConnect()
    {
        try
        {
            HandleConnectResult(
                 session.TryConnectAndLogin(
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
            attemptingConnection = false;
        }
    }

    /// <summary>
    /// handle the connection result and do things
    /// </summary>
    /// <param name="result"></param>
    private void HandleConnectResult(LoginResult result)
    {
        string outText;
        if (result.Successful)
        {
            var success = (LoginSuccessful)result;

            ServerData.SetupSession(success.SlotData, session.RoomState.Seed);
            Authenticated = true;
            //session.Socket.SendPacket(new GetDataPackagePacket() { Games = ["Book of Hours"] });

            DeathLinkHandler = new(session.CreateDeathLinkService(), ServerData.SlotName);
#if NET35
            session.Locations.CompleteLocationChecksAsync(null, ServerData.CheckedLocations.ToArray());
#else
            session.Locations.CompleteLocationChecksAsync(ServerData.CheckedLocations.ToArray());
#endif
            outText = $"Successfully connected to {ServerData.Uri} as {ServerData.SlotName}!";

            ArchipelagoConsole.LogMessage(outText);
        }
        else
        {
            var failure = (LoginFailure)result;
            outText = $"Failed to connect to {ServerData.Uri} as {ServerData.SlotName}.";
            outText = failure.Errors.Aggregate(outText, (current, error) => current + $"\n    {error}");

            Melon<Mod>.Logger.Error(outText);

            Authenticated = false;
            Disconnect();
        }

        ArchipelagoConsole.LogMessage(outText);
        attemptingConnection = false;
    }

    /// <summary>
    /// something went wrong, or we need to properly disconnect from the server. cleanup and re null our session
    /// </summary>
    public void Disconnect()
    {
        Melon<Mod>.Logger.Msg("disconnecting from server...");
#if NET35
        session?.Socket.Disconnect();
#else
        session?.Socket.DisconnectAsync();
#endif
        SetdownSession(session);
        session = null;
        Authenticated = false;
    }

    private void SetdownSession(ArchipelagoSession session)
    {
        session.MessageLog.OnMessageReceived -= message => ArchipelagoConsole.LogMessage(message.ToString());
        session.Items.ItemReceived -= OnItemReceived;
        session.Socket.ErrorReceived -= OnSessionErrorReceived;
        session.Socket.SocketClosed -= OnSessionSocketClosed;
        Patch_TransitionToState.TransitionToState_Prefix -= OnPrefix;
    }

    public void SendMessage(string message)
    {
        session.Socket.SendPacketAsync(new SayPacket { Text = message });
    }

    /// <summary>
    /// we received an item so reward it here
    /// </summary>
    /// <param name="helper">item helper which we can grab our item from</param>
    private void OnItemReceived(ReceivedItemsHelper helper)
    {
        var receivedItem = helper.DequeueItem();

        //if (helper.Index <= ServerData.Index) return;

        ServerData.Index++;

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