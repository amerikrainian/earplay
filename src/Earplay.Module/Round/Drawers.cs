using System.Collections.Generic;
using Earplay.Core;
using Earplay.Core.Graph;
using Earplay.Core.Screens;
using Earplay.Core.Strings;
using Earplay.Core.UI;
using Earplay.Module.Interop;
using Earplay.Module.UI;
using TMPro;
using UnityEngine;

namespace Earplay.Module.Round
{
    /// <summary>
    /// The modifiers the player owns (<c>BonusInventory.allModifierStubs</c> with
    /// <c>hasAPurchasedModifer</c>, in the drawer's order, which is the order they score in): each its
    /// stub text ("Refresh on New Round"), its score badge when it shows one, "disabled" when greyed
    /// out; the card's full description (<c>shopDescription</c>, then its <c>additionalDescription</c>)
    /// is the control buffer. Enter slides out the game's Sell (<c>ToggleCard</c>) and lands on it, listed
    /// right after the card while out ("Sell, +1", the refreshes it gives); Enter on it sells
    /// (<c>SellStub</c>), Enter on the card again slides it back. The drawer need not be open: its buttons answer either way.
    /// </summary>
    internal sealed class ModifiersSection : ScreenSection
    {
        public override void Build(GraphBuilder b)
        {
            var inv = BonusInventory.Instance;
            if (inv == null) return;
            var owned = new List<ModifierStubScript>();
            foreach (var stub in inv.allModifierStubs)
                if (stub != null && stub.hasAPurchasedModifer && stub.associatedModifier != null) owned.Add(stub);
            if (owned.Count == 0) return;
            owned.Sort((x, y) => x.transform.GetSiblingIndex().CompareTo(y.transform.GetSiblingIndex()));

            b.BeginStop("modifiers");
            b.PushContext(Strings.RoundModifiers);
            foreach (var stub in owned)
            {
                var s = stub;
                // Keyed by the stub's own name: a move renumbers allModifierStubs.
                string id = ModifierMove.Id(s);
                b.AddItem(ControlId.Structural(id), new NodeVtable
                {
                    ControlType = ControlTypes.Button,
                    Announcements = new List<NodeAnnouncement> { GameNodes.LabelPart(() => Line(s)) },
                    SearchText = () => Text(Private.Get<TMP_Text>(s, "myDescription")),
                    Details = () => Drawers.Description(s.associatedModifier),
                    OnDrag = () => ModifierMove.Press(s),
                    OnActivate = () =>
                    {
                        bool wasShowing = s.sellIconIsShowing;
                        s.ToggleCard();
                        // Sliding out: focus goes to the Sell it uncovered, once the slide ends (the game
                        // sets sellIconIsShowing when its tween completes).
                        if (!wasShowing) LandOnSell(s, id + ":sell", 20);
                    },
                    OnSecondary = RoundScreen.RemoveLast,
                });
                if (s.sellIconIsShowing && s.mySellButton != null)
                {
                    b.AddItem(ControlId.Structural(id + ":sell"), GameNodes.Button(() => SellLine(s), () =>
                    {
                        s.SellStub();
                        Later.Frames(40, () =>
                        {
                            string line = Round.RefreshesLine();
                            if (line != null) Speech.Say(line, interrupt: true);
                        }, "sold");
                    }, () => s.mySellButton.interactable));
                }
            }
            b.PopContext();
        }

        private static void LandOnSell(ModifierStubScript s, string sellId, int tries)
        {
            Later.Frames(3, () =>
            {
                if (s == null) return;
                if (s.sellIconIsShowing) Navigation.MoveTo(ControlId.Structural(sellId));
                else if (tries > 0 && s.stubIsMoving) LandOnSell(s, sellId, tries - 1);
            }, "sell");
        }

        private static string Line(ModifierStubScript s)
        {
            var parts = new List<string>();
            string text = Text(Private.Get<TMP_Text>(s, "myDescription"));
            if (text != null) parts.Add(text);
            var badge = Private.Get<GameObject>(s, "wordScoreArea");
            if (badge != null && badge.activeInHierarchy)
            {
                string score = Text(Private.Get<TMP_Text>(s, "wordScoreText"));
                if (score != null) parts.Add(score);
            }
            var disabled = Private.Get<GameObject>(s, "disabledOverlay");
            if (disabled != null && disabled.activeInHierarchy) parts.Add(Strings.StateDisabled);
            if (ReferenceEquals(ModifierMove.Held, s)) parts.Add(Strings.TileHeld);
            return string.Join(", ", parts);
        }

        // "Sell, +1": the button's two lines.
        private static string SellLine(ModifierStubScript s)
            => string.Join(", ", GameNodesText.Lines(Private.Get<TMP_Text>(s, "sellAmount")?.text ?? Strings.ModifierSell));

        internal static string Text(TMP_Text t)
        {
            if (t == null || string.IsNullOrWhiteSpace(t.text)) return null;
            var lines = GameNodesText.Lines(t.text);
            return lines.Count > 0 ? string.Join(", ", lines) : null;
        }
    }

    /// <summary>
    /// The upgrades the player owns (<c>BonusInventory.allUpgradeStubs</c> with
    /// <c>hasAPurchasedUpgrade</c>): each its stub text and the uses it has left
    /// (<c>UpgradeGetNumberLeft</c> of <c>UpgradeMaximum</c> plus <c>extraUpgradePoints</c>, the dots);
    /// the card's full description is the control buffer. Enter is the game's keyboard press on the
    /// stub: a tile upgrade starts the game's "Pick a tile to upgrade" (<c>PlayerClickedUpgrade</c>; then
    /// Enter on a board or word tile applies it, through the same press on the tile, and Escape
    /// cancels); a button upgrade fires (<c>TriggerBonusButtonScript</c>, which the game's own press
    /// reaches only in keyboard or controller mode). Space with a tile picked up drops it on the upgrade
    /// (<see cref="TileSwap.DropOnUpgrade"/>), the game's drag-and-drop route. An upgrade that makes a
    /// glass copy (<c>glassTile</c>, parked in its slot) names it ("glass E, 1 point, ready"), and Enter
    /// is then the game's press on that slot: the glass tile into the word, or back; dropping the glass
    /// tile back on its upgrade with Space breaks it, as the game's drag does. Either way the tile is
    /// re-read once the upgrade is done (<see cref="UpgradeConfirm"/>).
    /// </summary>
    internal sealed class UpgradesSection : ScreenSection
    {
        public override void Build(GraphBuilder b)
        {
            var inv = BonusInventory.Instance;
            if (inv == null || inv.allUpgradeStubs == null) return;
            var owned = new List<UpgradeStubScript>();
            foreach (var stub in inv.allUpgradeStubs)
                if (stub != null && stub.hasAPurchasedUpgrade && stub.associatedBonus != null) owned.Add(stub);
            if (owned.Count == 0) return;

            b.BeginStop("upgrades");
            b.PushContext(Strings.RoundUpgrades);
            foreach (var stub in owned)
            {
                var s = stub;
                b.AddItem(ControlId.Structural("upgrade:" + inv.allUpgradeStubs.IndexOf(s)), new NodeVtable
                {
                    ControlType = ControlTypes.Button,
                    Announcements = new List<NodeAnnouncement> { GameNodes.LabelPart(() => Line(s)) },
                    SearchText = () => ModifiersSection.Text(Private.Get<TMP_Text>(s, "myDescription")),
                    Details = () => Drawers.Description(s.associatedBonus),
                    OnActivate = () => Use(s),
                    OnSecondary = RoundScreen.RemoveLast,
                    // Space with a tile picked up drops it here (the game's drag onto the upgrade).
                    OnDrag = () => TileSwap.DropOnUpgrade(s),
                });
            }
            b.PopContext();
        }

        private static string Line(UpgradeStubScript s)
        {
            var parts = new List<string>();
            string text = ModifiersSection.Text(Private.Get<TMP_Text>(s, "myDescription"));
            if (text != null) parts.Add(text);
            var bonus = s.associatedBonus;
            int extra = BonusInventory.Instance != null ? BonusInventory.Instance.extraUpgradePoints : 0;
            parts.Add(Strings.UsesLeft(bonus.UpgradeGetNumberLeft(), bonus.UpgradeMaximum + extra));
            if (ReferenceEquals(ControlManagerScript.Instance?.stubAcceptingATile, s) && ControlManagerScript.Instance.inUpgradeTileSelectMode)
                parts.Add(Strings.UpgradePicking);
            // The glass copy an upgrade made, parked in its slot until it goes into a word.
            var glass = Glass(s);
            if (glass != null)
                parts.Add(glass.tileIsInSubmissionQueue ? Strings.GlassInWord(Round.TileLine(glass)) : Strings.GlassHere(Round.TileLine(glass)));
            return string.Join(", ", parts);
        }

        private static void Use(UpgradeStubScript s)
        {
            if (!Round.CanInteract) return;
            // With a glass copy waiting, the press is the game's on the upgrade's slot: the glass tile
            // goes into the word (or back from it).
            var glass = Glass(s);
            var slot = s.GetComponentInChildren<TileHolderScript>(false);
            if (glass != null && slot != null) { slot.ControllerInteractTile(); return; }
            if (s.associatedBonus.HasButton) s.TriggerBonusButtonScript();
            else s.PlayerClickedUpgrade();
        }

        private static TileScript Glass(UpgradeStubScript s)
        {
            var glass = s.glassTileHasBeenCreated ? s.glassTile : null;
            return glass != null && !glass.fullySpent ? glass : null;
        }
    }

    /// <summary>
    /// Reordering the modifiers by keyboard (Space), their order being the scoring order: the first
    /// press on a modifier picks it up (noted only), a press on another moves it into that one's place,
    /// the end of the game's own drag (the stub's sibling index set to the target's under
    /// <c>BonusInventory.modifierParent</c>, then <c>RefreshStubList</c>, which rebuilds
    /// <c>ownedModifiers</c> in that order). Space on the held one, or Escape, puts it down. Focus
    /// follows the moved modifier, and the landing re-reads it.
    /// </summary>
    internal static class ModifierMove
    {
        private static ModifierStubScript _held;

        public static ModifierStubScript Held => _held != null ? _held : null;
        public static bool Holding => Held != null;

        public static string Id(ModifierStubScript stub) => "modifier:" + stub.gameObject.name;

        public static void Press(ModifierStubScript target)
        {
            if (target == null || !Round.CanInteract || TileSwap.Holding) return;
            var held = Held;
            if (held == null)
            {
                if (!target.hasAPurchasedModifer || target.stubIsMoving) return;
                _held = target;
                Speech.Say(Strings.PickedUp(ModifiersSection.Text(Private.Get<TMP_Text>(target, "myDescription"))), interrupt: true);
                return;
            }
            if (ReferenceEquals(held, target)) { Cancel(); return; }
            Move(held, target);
        }

        public static void Cancel()
        {
            var held = Held;
            _held = null;
            if (held != null) Speech.Say(Strings.PutDown(ModifiersSection.Text(Private.Get<TMP_Text>(held, "myDescription"))), interrupt: true);
        }

        public static void Reset() => _held = null;

        private static void Move(ModifierStubScript held, ModifierStubScript target)
        {
            _held = null;
            var inv = BonusInventory.Instance;
            if (inv == null || inv.modifierParent == null) return;
            try
            {
                held.transform.SetSiblingIndex(target.transform.GetSiblingIndex());
                inv.RefreshStubList();
            }
            catch (System.Exception e)
            {
                CoreLog.Warning("ModifierMove: move failed: " + e);
                return;
            }
            // Focus follows it, and the landing re-reads it in its new place.
            if (!Navigation.MoveTo(ControlId.Structural(Id(held)))) Navigation.AnnounceCurrent();
        }
    }

    internal static class Drawers
    {
        /// <summary>A card's full description: its shop text, then the game's longer description of
        /// what it mentions.</summary>
        public static IEnumerable<string> Description(BaseBonus bonus)
        {
            var lines = new List<string>();
            if (bonus == null) return lines;
            lines.AddRange(GameNodesText.Lines(bonus.shopDescription));
            var d = BonusDescriptions.Instance;
            if (d != null && bonus.additionalDescription != BonusDescriptions.Description.None)
                lines.AddRange(GameNodesText.Lines(d.ShopDescription(bonus.additionalDescription)));
            return lines;
        }
    }
}
