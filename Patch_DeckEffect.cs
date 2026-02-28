//using HarmonyLib;
//using MelonLoader;
//using SecretHistories.Abstract;
//using SecretHistories.Core;
//using SecretHistories.Entities;
//using SecretHistories.Logic;
//using SecretHistories.States;
//using SecretHistories.UI;

//public class DrawnCardFromDeckEventArgs(string labl) : EventArgs
//{
//    public string Label { get; } = labl;
//}

//[HarmonyPatch(typeof(Dealer))]
//public static class Patch_RecipeCompletionEffectCommand
//{
//    public static event Action<DrawnCardFromDeckEventArgs> DealerDeal_Postfix = delegate { };

//    [HarmonyPostfix]
//    [HarmonyPatch(nameof(Dealer.Deal), typeof(DeckSpec), typeof(IHasCardPiles))]
//    static void Postfix(Token __result)
//    {
//        /// __result.PayloadId           ->      "!precursor.mem.foresight_141"
//        /// __result.PayloadEntityId     ->      precursor.mem.foresight
//        // Precursors are NOT the actual element

//        ElementStack _payload = (SecretHistories.UI.ElementStack)__result.Payload;
//        //Element _element;
//        string _drawnCardLabel;
//        if (_payload.Element.Decays)
//        {
//            // 'DecayTo' is just the id string, not the ingame Label
//            Element _el = Mod.Compendium.GetEntityById<Element>(_payload.Element.DecayTo);
//            _drawnCardLabel = _el.Label;
//        }
//        else
//        {
//            _drawnCardLabel = _payload.Element.Label;
//        }

//        DealerDeal_Postfix.Invoke(new DrawnCardFromDeckEventArgs(_drawnCardLabel));
//    }
//}