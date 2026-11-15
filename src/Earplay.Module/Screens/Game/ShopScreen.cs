using System.Collections.Generic;
using Earplay.Core;
using Earplay.Core.Graph;
using Earplay.Core.Strings;
using Earplay.Core.UI;
using Earplay.Module.Interop;
using Earplay.Module.Round;
using Earplay.Module.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Screen = Earplay.Core.Screens.Screen;

namespace Earplay.Module.Screens.Game
{
    /// <summary>
    /// The shop between rounds (<c>ShopManager</c>, while <c>shopIsShowing</c>): its title ("Round 1
    /// Complete") names the screen. One stop, top to bottom: the plays the round brought ("+4 Plays"),
    /// the cards (<c>upgradeCards</c>, the shown ones) as one row, as drawn: each its kind (Modifier,
    /// Upgrade, Gift, Special), rarity and description, an upgrade's uses (its <c>UpgradeMaximum</c>,
    /// the dots), and why a card cannot be bought ("All Modifier Slots Filled"); the game's longer
    /// description of what the card mentions (<c>additionalDescription</c>) is the control buffer.
    /// Enter buys (<c>BuyThisCard</c>: one purchase, then the next round). Then Re-Roll
    /// ("-1 Plays") and Skip ("+2 Refresh") as the game draws them, the letter bag (the mod's
    /// <see cref="BagScreen"/>), Peek (the game's <c>TogglePeek</c>: the shop fades and the board shows,
    /// read by <see cref="PeekScreen"/> until Escape brings the shop back), and the plays line. A
    /// re-roll reads the new cards once they are dealt.
    /// </summary>
    public sealed class ShopScreen : Screen
    {
        public override string Key => "game.shop";
        // A peek steps the shop aside: back from it, focus is where it was (on Peek). Closing the shop
        // for the next round drops it, so the next shop starts on its first card, not on the place of
        // the card bought last time (read at pop time: TogglePeek sets peeking before the shop steps aside).
        public override bool KeepStateOnPop => ShopManager.Instance != null && ShopManager.Instance.peeking;
        // A fresh shop lands on its first card (the plays line above it is one Up away).
        public override ControlId InitialFocusNode => ControlId.Structural("shop:card0");

        private static ShopManager Shop()
        {
            var shop = ShopManager.Instance;
            return shop != null && shop.shopIsShowing && !shop.peeking && shop.shopScreen != null && shop.shopScreen.activeInHierarchy ? shop : null;
        }

        public override bool IsActive()
            => Shop() != null && GamepadControl.Instance != null
               && GamepadControl.Instance.currentControlZone == GamepadControl.ControlZone.Shop;

        public override void Build(GraphBuilder b)
        {
            var shop = Shop();
            if (shop == null) return;
            b.PushContext(Title(shop) ?? Strings.ScreenShop, null, positions: false);

            var cards = Cards(shop);
            if (cards.Count > 0)
            {
                var plays = Private.Get<TMP_Text>(shop, "livesOnNewRoundPop");
                if (plays != null && plays.gameObject.activeInHierarchy && !string.IsNullOrWhiteSpace(plays.text))
                    b.AddItem(ControlId.Structural("shop:plays_gained"), GameNodes.Text(() => plays.text));
                b.StartRow();
                for (int i = 0; i < cards.Count; i++)
                {
                    var card = cards[i];
                    b.AddItem(ControlId.Structural("shop:card" + i), Card(card));
                }
                b.EndRow();
            }

            AddLabelled(b, "shop:reroll", Private.Get<Button>(shop, "buttonReRoll"), shop.reRollButtonText, () => SayCardsWhenDealt(DealTries));
            AddLabelled(b, "shop:skip", Private.Get<Button>(shop, "buttonSkip"), shop.skipButtonText, null);
            b.AddItem(ControlId.Structural("shop:bag"), GameNodes.Button(() => Strings.ToolBag, BagScreen.Open));
            b.AddItem(ControlId.Structural("shop:peek"), GameNodes.Button(() => Strings.ShopPeek, () =>
            {
                if (ShopManager.Instance != null) ShopManager.Instance.TogglePeek();
            }));
            if (Round.Round.PlaysLine() != null)
                b.AddItem(ControlId.Structural("shop:plays"), GameNodes.Text(Round.Round.PlaysLine));
            b.PopContext();
        }

        // The shop's title as shown: "Round 1 Complete" (shopTitleText), or Quick Play's own label
        // ("Next Round...", a separate label the title field does not hold).
        private static string Title(ShopManager shop)
        {
            var title = shop.shopTitleText;
            if (title != null && title.gameObject.activeInHierarchy && !string.IsNullOrWhiteSpace(title.text)) return title.text;
            var labels = shop.shopScreen.transform.Find("Elements/Labels");
            if (labels == null) return null;
            foreach (var text in labels.GetComponentsInChildren<TMP_Text>(false))
            {
                if (text.GetComponentInParent<Button>() != null) continue;
                var lines = GameNodesText.Lines(text.text);
                if (lines.Count > 0) return lines[0];
            }
            return null;
        }

        private static List<BonusCardScript> Cards(ShopManager shop)
        {
            var list = new List<BonusCardScript>();
            if (shop.upgradeCards == null) return list;
            foreach (var card in shop.upgradeCards)
                if (card != null && card.gameObject.activeInHierarchy && card.associatedBonus != null) list.Add(card);
            return list;
        }

        // "Upgrade, Common, Hold one tile and Refresh all of the others, 4 uses".
        private static string CardLine(BonusCardScript card)
        {
            var parts = new List<string>();
            Add(parts, Private.Get<TMP_Text>(card, "headerText"));
            // Quick Play's cards repeat the kind where the rarity goes ("Modifier, Modifier"): once.
            var rarity = Private.Get<TMP_Text>(card, "rarityText");
            if (rarity != null && (parts.Count == 0 || rarity.text.Trim() != parts[0])) Add(parts, rarity);
            Add(parts, Private.Get<TMP_Text>(card, "myDescription"));
            var bonus = card.associatedBonus;
            if (bonus != null && bonus.BonusType == BaseBonus.Type.Upgrade && bonus.UpgradeMaximum > 0)
                parts.Add(Strings.CardUses(bonus.UpgradeMaximum));
            var filled = Private.Get<GameObject>(card, "allFilledGO");
            if (filled != null && filled.activeInHierarchy) Add(parts, Private.Get<TMP_Text>(card, "allFilledText"));
            return string.Join(", ", parts);
        }

        private static void Add(List<string> parts, TMP_Text text)
        {
            if (text != null && !string.IsNullOrWhiteSpace(text.text)) parts.Add(text.text.Trim());
        }

        private static NodeVtable Card(BonusCardScript card)
        {
            var button = Private.Get<Button>(card, "myButton");
            System.Func<string> label = () => CardLine(card);
            var vt = button != null
                ? GameNodes.Button(button, label)
                : GameNodes.Text(label);
            vt.Details = () => CardDetails(card);
            return vt;
        }

        // The game's longer description of what the card mentions (a special tile, a tool).
        private static IEnumerable<string> CardDetails(BonusCardScript card)
        {
            var bonus = card.associatedBonus;
            var descriptions = BonusDescriptions.Instance;
            if (bonus == null || descriptions == null || bonus.additionalDescription == BonusDescriptions.Description.None)
                return new List<string>();
            return GameNodesText.Lines(descriptions.ShopDescription(bonus.additionalDescription));
        }

        // A two-part button ("Re-Roll" beside "-1 Plays"): its caption, then the cost the game draws.
        private static void AddLabelled(GraphBuilder b, string key, Button button, TMP_Text caption, System.Action after)
        {
            if (!GameNodes.IsShown(button)) return;
            // The caption is the button's "Left Text", the cost its other text (the game's own field
            // for the button's text points at the cost).
            System.Func<string> label = () =>
            {
                var parts = new List<string>();
                var texts = button.GetComponentsInChildren<TMP_Text>(false);
                foreach (var t in texts)
                    if (t.gameObject.name == "Left Text") Add(parts, t);
                foreach (var t in texts)
                    if (t.gameObject.name != "Left Text") Add(parts, t);
                return parts.Count > 0 ? string.Join(", ", parts) : button.gameObject.name;
            };
            b.AddItem(ControlId.Structural(key), GameNodes.Button(label, () =>
            {
                if (!button.interactable) return;
                button.onClick.Invoke();
                after?.Invoke();
            }, () => button.interactable));
        }

        // A re-roll picks the new cards at once (BonusStore.nextThreeUpgrades), then SpinCards turns
        // them over one at a time, half a second apart, each filled mid-spin (FillInDetails sets its
        // associatedBonus): read them once every shown card holds the one dealt to it.
        private const int DealTries = 120; // 3 frames apart: about six seconds at 60fps

        private static void SayCardsWhenDealt(int tries)
        {
            var shop = Shop();
            if (shop == null) return;
            if (!Dealt(shop))
            {
                if (tries > 0)
                {
                    Later.Frames(3, () => SayCardsWhenDealt(tries - 1), "reroll");
                    return;
                }
                CoreLog.Warning("shop: re-rolled cards still not dealt after waiting; reading them as they are");
            }
            SayCards();
        }

        private static bool Dealt(ShopManager shop)
        {
            var store = BonusStore.Instance;
            if (store == null || store.nextThreeUpgrades == null)
            {
                CoreLog.Warning("shop: no BonusStore to check the re-rolled cards against");
                return true;
            }
            var next = store.nextThreeUpgrades;
            for (int i = 0; i < next.Count && i < shop.upgradeCards.Count; i++)
            {
                var card = shop.upgradeCards[i];
                if (card != null && card.gameObject.activeInHierarchy && !ReferenceEquals(card.associatedBonus, next[i]))
                    return false;
            }
            return true;
        }

        // After a re-roll: the new cards, one line.
        private static void SayCards()
        {
            var shop = Shop();
            if (shop == null) return;
            var lines = new List<string>();
            foreach (var card in Cards(shop)) lines.Add(CardLine(card));
            if (lines.Count > 0) Speech.Say(string.Join(". ", lines));
        }
    }
}
