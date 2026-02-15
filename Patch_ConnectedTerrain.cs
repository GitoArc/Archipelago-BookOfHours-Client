using HarmonyLib;
using SecretHistories.Entities;
using SecretHistories.Tokens.Payloads;

[HarmonyPatch(typeof(ConnectedTerrain))]
public static class Patch_ConnectedTerrain
{
    public static ConnectedTerrain[] Patch_GetConnectedTerrain(this ConnectedTerrain terrain)
    {
        var a = Traverse.Create(terrain).Field("connectedRooms").GetValue() as ConnectedTerrain[];
        //again, the 'terrain.' prefix is missing; BUT the Id has protected access
        //better to NOT MUCK with it since the game may use the original string; just keep in mind when json-dump
        return a;
    }
}