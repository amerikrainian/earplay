using System.Collections.Generic;
using Earplay.Core;
using Earplay.Core.Graph;
using Earplay.Core.Strings;
using Earplay.Core.UI;
using Earplay.Module.Interop;
using Earplay.Module.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Screen = Earplay.Core.Screens.Screen;

namespace Earplay.Module.Round
{
    /// <summary>
    /// The Swap Tile picker (<c>PickTileManager</c>), opened by a "Pick a Vowel" or "Pick a Consonant"
    /// upgrade on a tile (the game's control zone becomes TilePicker). Its title ("Swap Tile") names
    /// the screen, then the tile being replaced ("replacing E, 1 point"), then the letters as one row:
    /// each its letter and points (<c>tileTMPs</c> / <c>scoreTMPs</c>), disabled when the bag holds
    /// none of it (<c>DoesBagHave</c>); Enter picks it (the button's own <c>PickTile</c>), and the tile
    /// is re-read once it has changed (<see cref="UpgradeConfirm"/>). Then Cancel; Escape cancels too
    /// (<c>PickTileManager.Cancel</c>: the tile goes back unchanged). A modal: the keyboard is the mod's.
    /// </summary>
    public sealed class PickerScreen : Screen
    {
        public override string Key => "game.picker";
        public override int Layer => 30;
        public override bool Exclusive => true;

        private static PickTileManager Picker()
        {
            var picker = PickTileManager.Instance;
            var gamepad = GamepadControl.Instance;
            if (picker == null || gamepad == null || gamepad.currentControlZone != GamepadControl.ControlZone.TilePicker) return null;
            var group = Private.Get<CanvasGroup>(picker, "pickerCG");
            return group != null && group.interactable ? picker : null;
        }

        public override bool IsActive() => Picker() != null;

        public override void Build(GraphBuilder b)
        {
            var picker = Picker();
            if (picker == null) return;
            var title = TitleOf(picker);
            b.PushContext(title ?? Strings.ScreenPicker, Strings.RoleDialog, positions: false);

            var tile = Private.Get<TileScript>(picker, "tileIWillChange");
            if (tile != null)
                b.AddItem(ControlId.Structural("picker:replacing"), GameNodes.Text(() => Strings.PickerReplacing(Round.TileLine(tile))));

            var buttons = Private.Get<List<Button>>(picker, "tileButtons");
            var letters = Private.Get<List<TMP_Text>>(picker, "tileTMPs");
            var scores = Private.Get<List<TMP_Text>>(picker, "scoreTMPs");
            if (buttons != null && letters != null)
            {
                var shown = new List<int>();
                for (int i = 0; i < buttons.Count && i < letters.Count; i++)
                    if (GameNodes.IsShown(buttons[i]) && letters[i] != null && letters[i].gameObject.activeInHierarchy
                        && !string.IsNullOrWhiteSpace(letters[i].text))
                        shown.Add(i);
                if (shown.Count > 0)
                {
                    b.StartRow();
                    foreach (int i in shown)
                    {
                        var button = buttons[i];
                        var letter = letters[i];
                        var score = scores != null && i < scores.Count ? scores[i] : null;
                        b.AddItem(ControlId.Structural("picker:" + i), GameNodes.Button(() => LetterLine(letter, score), () =>
                        {
                            if (button.interactable) button.onClick.Invoke();
                        }, () => button.interactable));
                    }
                    b.EndRow();
                }
            }

            var cancel = CancelButton(picker);
            if (GameNodes.IsShown(cancel)) b.AddItem(ControlId.Structural("picker:cancel"), GameNodes.Button(cancel));
            b.PopContext();
        }

        // "B, 3 points".
        private static string LetterLine(TMP_Text letter, TMP_Text score)
        {
            string text = letter.text.Trim();
            return score != null && int.TryParse(score.text, out int points) ? text + ", " + Strings.Points(points) : text;
        }

        // The picker's own title, the TMP named "Label" under it ("Swap Tile").
        private static string TitleOf(PickTileManager picker)
        {
            foreach (var t in picker.GetComponentsInChildren<TMP_Text>(false))
                if (t.gameObject.name == "Label" && !string.IsNullOrWhiteSpace(t.text)) return t.text.Trim();
            return null;
        }

        // The captioned button that is not a letter: Cancel.
        private static Button CancelButton(PickTileManager picker)
        {
            var letters = Private.Get<List<Button>>(picker, "tileButtons");
            foreach (var button in picker.GetComponentsInChildren<Button>(false))
                if (letters == null || !letters.Contains(button)) return button;
            return null;
        }

        public override IEnumerable<ElementAction> GetActions()
        {
            yield return new ElementAction(ActionIds.Back, Strings.Get("bind.ui.back"), _ =>
            {
                var picker = Picker();
                if (picker != null) picker.Cancel();
            });
        }
    }
}
