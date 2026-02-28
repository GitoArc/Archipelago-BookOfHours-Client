using HarmonyLib;
using SecretHistories.Entities;
using SecretHistories.Enums;
using SecretHistories.States;


public class TransitionToStateEventArgs(Situation instance, SituationState newState)
{
    public Situation Situation { get; } = instance;
    public SituationState SituationState { get; } = newState;
}


[HarmonyPatch(typeof(Situation))]
public static class Patch_TransitionToState
{
    public static event Action<TransitionToStateEventArgs> StateIsRequiExec_Prefix = delegate { };
    public static event Action<TransitionToStateEventArgs> StateIsRequiExec_Postfix = delegate { };

    [HarmonyPrefix]
    [HarmonyPatch(nameof(Situation.TransitionToState))]
    static void Prefix(Situation __instance, SituationState newState)
    {
        if (__instance == null || newState == null
            || newState.Identifier != StateEnum.RequiringExecution)
            return;

        //var actionId = recipe.ActionId;  //eg "terrain.unlock"
        //var id = recipe.Id;              //eg "terrain.gatehouse"
        //var labl = recipe.Label;         //eg "Watchman\'s Tower: Gatehouse"

        StateIsRequiExec_Prefix.Invoke(new TransitionToStateEventArgs(__instance, newState));
    }

    [HarmonyPostfix]
    [HarmonyPatch(nameof(Situation.TransitionToState))]
    static void Postfix(Situation __instance, SituationState newState)
    {
        if (__instance == null || newState == null
            || newState.Identifier != StateEnum.RequiringExecution)
            return;
        // 'StateEnum.RequiringExecution' seems to act as a 'finalizer'?
        //  Some situations  (like looping recipes or terrain.unlocks)  never get 'StateEnum.Complete'
        //  but 'StateEnum.RequiringExecution' always happens (at least so far)

        StateIsRequiExec_Postfix.Invoke(new TransitionToStateEventArgs(__instance, newState));
    }
}