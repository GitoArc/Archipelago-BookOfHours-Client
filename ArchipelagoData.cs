using Newtonsoft.Json;


public class ArchipelagoData
{
    public string Uri { get; set; }
    public string SlotName { get; set; }
    public string Password { get; set; }
    public int Latest_Index_from_Server { get; set; }
    public HashSet<long> CheckedLocations { get; private set; } = [];

    public int Progression_Memories_TotalRemembered { get; set; } = 0;
    public int Progression_Souls_TotalAcquired { get; set; } = 0;
    public int Progression_Wisdoms_TotalCommitted { get; set; } = 0;

    /// <summary>
    /// seed for this archipelago data. Can be used when loading a file to verify the session the player is trying to
    /// load is valid to the room it's connecting to.
    /// </summary>
    public string Seed { get; private set; }

    public Dictionary<string, object> SlotData { get; private set; } = null;

    public bool NeedSlotData => SlotData == null;

    public ArchipelagoData()
    {
        Uri = "localhost";
        SlotName = "Player1";
    }

    public ArchipelagoData(string uri, string slotName, string password)
    {
        Uri = uri;
        SlotName = slotName;
        Password = password;
    }

    /// <summary>
    /// assigns the slot data and seed to our data handler. any necessary setup using this data can be done here.
    /// </summary>
    /// <param name="roomSlotData">slot data of your slot from the room</param>
    /// <param name="roomSeed">seed name of this session</param>
    public void SetupSession(Dictionary<string, object> roomSlotData, string roomSeed)
    {
        SlotData = roomSlotData;
        Seed = roomSeed;
    }

    /// <summary>
    /// returns the object as a json string to be written to a file which you can then load
    /// </summary>
    /// <returns></returns>
    public override string ToString()
    {
        return JsonConvert.SerializeObject(this);
    }
}