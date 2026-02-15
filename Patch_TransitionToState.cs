using HarmonyLib;
using MelonLoader;
using SecretHistories.Entities;
using SecretHistories.States;
using SecretHistories.UI;

public class RecipeSuccessEventArgs(Situation instance, SituationState newState) : EventArgs
{
    public Situation Situation { get; } = instance;
    public SituationState SituationState { get; } = newState;
}

[HarmonyPatch(typeof(Situation))]
public static class Patch_TransitionToState
{
    public static event Action<RecipeSuccessEventArgs> TransitionToState_Prefix = delegate { };
    public static event Action<RecipeSuccessEventArgs> TransitionToState_Postfix = delegate { };

    [HarmonyPrefix]
    [HarmonyPatch(nameof(Situation.TransitionToState))]
    static void Prefix(Situation __instance, SituationState newState)
    {
        var spheres = Watchman.Get<HornedAxe>().GetSpheres();
        if (__instance == null) return;
        if (newState == null) return;

        if (__instance.GetCurrentRecipe().ActionId == "terrain.unlock")
        {
            int x = 0;
        }
        //room/terrain unlocks have Identifier == RequiresExecution tho...
        //if (newState.Identifier == SecretHistories.Enums.StateEnum.Starting) return;

        //if (__instance.CurrentRecipeId == "study.mastering.no") return;

        Melon<Mod>.Logger.Msg($"TransitionToState.Prefix(): {__instance.Id} - {newState}");

        /// award item into game
        var r = __instance.GetCurrentRecipe();
        //r.Effects.TryAdd("florin", "1");
        //r.Effects.TryAdd("weather.gale", "1");
        var actionId = r.ActionId;  //eg "terrain.unlock"
        var id = r.Id;              //eg "terrain.gatehouse"
        var labl = r.Label;         //eg "Watchman\'s Tower: Gatehouse"


        TransitionToState_Prefix.Invoke(new RecipeSuccessEventArgs(__instance, newState));
    }

    [HarmonyPostfix]
    [HarmonyPatch(nameof(Situation.TransitionToState))]
    static void Postfix(Situation __instance, SituationState newState)
    {
        if (__instance?.CurrentRecipeId == "ap.recipe.reward") {
            //scrub the awarded ap-items (so they dont get repeatedly awarded)
            var r = __instance.GetCurrentRecipe();
            r.Effects = [];
            r.Notable = false;
            ///just to be safe it is applied:
            __instance.SetCurrentRecipe(r);
        }
        TransitionToState_Postfix.Invoke(new RecipeSuccessEventArgs(__instance, newState));
    }
}