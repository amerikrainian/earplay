using System.Collections.Generic;
using Earplay.Core;
using Earplay.Core.Graph;
using Earplay.Core.Strings;
using Earplay.Core.UI;
using Earplay.Module.UI;
using Screen = Earplay.Core.Screens.Screen;

namespace Earplay.Module.Round
{
    /// <summary>
    /// The letter bag (Enter on the Bag tool), read from the game's own lists rather than its drawer:
    /// <c>LetterBagManager.LetterBag</c> (what can still be drawn) and <c>SpentLetters</c> (used this
    /// round, back in the bag next round), each tile an id string (letter, type code, two digits of
    /// bonus points, two of special score: "ES0000"). Tiles that read alike are counted together,
    /// alphabetically: "A, 1 point: 6", "A, 1 point, emerald: 1". A modal of its own; Escape closes it.
    /// </summary>
    public sealed class BagScreen : Screen
    {
        private static bool s_open;

        public static void Open() => s_open = true;

        public override string Key => "game.bag";
        public override int Layer => 20;
        public override bool Exclusive => true;
        public override bool IsActive() => s_open && LetterBagManager.Instance != null;

        public override void Build(GraphBuilder b)
        {
            var bag = LetterBagManager.Instance;
            if (bag == null) return;
            b.BeginStop("bag");
            Group(b, "bag", Strings.BagRemaining(bag.LetterBag.Count), bag.LetterBag);
            if (bag.SpentLetters.Count > 0)
            {
                b.BeginStop("spent");
                Group(b, "spent", Strings.BagSpent(bag.SpentLetters.Count), bag.SpentLetters);
            }
        }

        private static void Group(GraphBuilder b, string key, string title, List<string> ids)
        {
            var counts = new SortedDictionary<string, int>(System.StringComparer.Ordinal);
            foreach (var id in ids)
            {
                string line = Describe(id);
                counts[line] = counts.TryGetValue(line, out int n) ? n + 1 : 1;
            }
            b.PushContext(title);
            foreach (var kv in counts)
            {
                string text = Strings.StatusPair(kv.Key, kv.Value.ToString());
                b.AddItem(ControlId.Structural(key + ":" + kv.Key), GameNodes.Text(() => text));
            }
            b.PopContext();
        }

        // "A, 1 point, emerald" from "AE0000".
        private static string Describe(string id)
        {
            if (string.IsNullOrEmpty(id)) return "?";
            string letter = id.Substring(0, 1);
            char type = id.Length > 1 ? id[1] : 'S';
            int bonus = 0;
            if (id.Length >= 4) int.TryParse(id.Substring(2, 2), out bonus);
            string name;
            switch (letter)
            {
                case "*": name = Strings.TileWildcard; break;
                case "+": name = Strings.TilePlus; break;
                case "!": name = Strings.TileExclamation; break;
                case "2": name = Strings.TileMirror; break;
                default:
                    name = char.IsDigit(letter[0]) && ComboTileManager.Instance != null
                        ? ComboTileManager.Instance.ComboText(letter[0] - '0', uppercase: false)
                        : letter;
                    break;
            }
            int points = (ScoreManager.Instance != null ? ScoreManager.Instance.GetTileScore(letter) : 0) + bonus;
            var parts = new List<string> { name, Strings.Points(points) };
            switch (type)
            {
                case 'G': parts.Add(Strings.TileGolden); break;
                case 'D': parts.Add(Strings.TileDiamond); break;
                case 'P': parts.Add(Strings.TilePotion); break;
                case 'F': parts.Add(Strings.TileDot); break;
                case 'E': parts.Add(Strings.TileEmerald); break;
                case 'T': parts.Add(Strings.TileGlass); break;
                case 'L': parts.Add(Strings.TileLockedPlain); break;
            }
            return string.Join(", ", parts);
        }

        public override IEnumerable<ElementAction> GetActions()
        {
            yield return new ElementAction(ActionIds.Back, Strings.Get("bind.ui.back"), _ => s_open = false);
        }

        public override void OnPop() => s_open = false;
    }
}
