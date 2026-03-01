using HarmonyLib;
using MelonLoader;
using SecretHistories.Enums;
using SecretHistories.Events;
using SecretHistories.Tokens.Payloads;
using System.ComponentModel;


[HarmonyPatch(typeof(WisdomNodeTerrain))]
public static class Patch_WisdomNodeTerrain
{
    public static event Action<string> WisdomNodeCommitted = delegate { };
    public static event Action<string> OutputTokenSpawned = delegate { };

    [HarmonyPostfix]
    [HarmonyPatch(nameof(WisdomNodeTerrain.OnTokensChangedForSphere))]
    static void OnTokensChangedForSphere_Postfix(WisdomNodeTerrain __instance, SphereContentsChangedEventArgs args)
    {
        // ! this will fire when you load a save - for every committed 

        // args.Change == TokenAdded --> A card was put into an empty slot... - this by itself is USELESS, since user can pull it back out
        //      ... OR SPHERE : There exists a "commitment" sphere - which only fires when the Token is CONFIRMED LOCKED in <- VERY NOT USELESS

        // args.Change == TokenChanged -->
        //      happens MULTIPLE TIMES when committing (bc the aspect-mutations? : "Committed" and "Attuned: xyz" get added, one Path-Aspect gets removed)
        //      OR when the "face-down card" gets spawned - I guess the card gets added in the normal/"face-up" state, and this get called because Veiling/Unveiling ("flipping") the card counts for TokenChange?
        //      OR when the "face-down card" gets clicked - this unveils the card; See line above ^

        // args.Change == TokenRemoved --> when the card/token gets removed from a sphere
        //      kinda useless bc I can determine everything I need from TokenAdded/TokenChanged

#if DEBUG
        string msg = args.Change switch
        {
            SphereContentChange.TokenAdded => $"TokenAdded - {args.Token.PayloadEntityId} - {args.Sphere}",
            SphereContentChange.TokenRemoved => $"TokenRemoved - {args.Token.PayloadEntityId} - {args.Sphere}",
            SphereContentChange.TokenChanged => $"TokenChanged - {args.Token.PayloadEntityId} - {args.Sphere}",
            _ => throw new InvalidEnumArgumentException(),
        };
        Melon<Mod>.Logger.Msg(msg);
#endif

        if (args.Change == SphereContentChange.TokenAdded
            && args.Sphere.name == "commitment" && args.Sphere.SphereCategory == SphereCategory.Commitment)
        {
            WisdomNodeCommitted.Invoke(__instance.Id);
        }
        else if (args.Change == SphereContentChange.TokenAdded
            && args.Sphere.name == "output" && args.Sphere.SphereCategory == SphereCategory.Threshold)
        {
            OutputTokenSpawned.Invoke(args.Token.Payload.Label);
        }
    }
}