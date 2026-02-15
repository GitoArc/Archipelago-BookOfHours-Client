using HarmonyLib;
using MelonLoader;
using SecretHistories.Entities;
using SecretHistories.Spheres;
using SecretHistories.Tokens.Payloads;
using SecretHistories.UI;
using UnityEngine.InputSystem.LowLevel;

[HarmonyPatch(typeof(WisdomNodeTerrain))]
public static class Patch_WisdomNodeTerrain
{
    [HarmonyPostfix]
    [HarmonyPatch(nameof(WisdomNodeTerrain.TryCommitToken))]
    static void Postfix(WisdomNodeTerrain __instance, Token token)
    {
        //bc the original method doesnt return antyhing, validating is bs
        //just assume it locked in?

        Melon<Mod>.Logger.Msg($"'{token.Payload.Label}' has been assigned to the Tree: '{__instance.Id}'");
        //this.CommitToken(token2, firstMatchingRecipe);
    }
}