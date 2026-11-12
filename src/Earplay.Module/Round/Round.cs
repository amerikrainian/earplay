using System;
using System.Collections.Generic;
using Earplay.Core.Strings;
using Earplay.Module.Interop;
using Earplay.Module.UI;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Earplay.Module.Round
{
    /// <summary>The kinds of thing a board cell holds.</summary>
    internal enum CellKind { Tile, Empty, LockedSlot, Backspace, Refresh, Bag, Submit, Shuffle, Unknown }

    /// <summary>One square of the board as drawn: its 1-based column and row (row 1 at the top), the
    /// holder the game keeps there, and what it holds now.</summary>
    internal sealed class Cell
    {
        public int Column, Row;
        public TileHolderScript Holder;
        public CellKind Kind;
        public Button Tool; // the tool's button, for a tool cell
        public TileScript Tile => Holder != null ? Holder.myTile : null;
    }

    /// <summary>
    /// The round as the game has it, read fresh on every call (nothing kept). The board is the tile
    /// grid as drawn (<c>GridManagerScript.tileHolders</c>' parent, a GridLayoutGroup of
    /// <c>constraintCount</c> columns: three bonus slots and Shuffle in its first column, the letter
    /// tiles in the rest), with the tool column (the "Grid of Tools" holders, top to bottom: Backspace,
    /// Refresh, Bag, Submit) drawn to its right as one more column, one tool per row. The word is the
    /// submission row (<c>SubmissionScript.tileHolderScripts</c>, the shown ones). Tools are told apart
    /// by their wiring, since their scripts all sit on one shared "Tools" object.
    /// </summary>
    internal static class Round
    {
        /// <summary>The round is taking words: the game's control zone is the letter grid (the
        /// Options drawer, the bag, a tutorial, the shop or the end screen move it elsewhere).</summary>
        public static bool Playing => Zone() == GamepadControl.ControlZone.LetterGrid;

        /// <summary>The round is on screen, perhaps under a drawer or a tutorial panel: the zone is the
        /// letter grid or one of the panels that open over it and return to it. Not the shop, the
        /// tile picker or the end screen, which replace it.</summary>
        public static bool OnScreen
        {
            get
            {
                var zone = Zone();
                return zone == GamepadControl.ControlZone.LetterGrid || zone == GamepadControl.ControlZone.Menu
                    || zone == GamepadControl.ControlZone.LetterBag || zone == GamepadControl.ControlZone.Tutorial;
            }
        }

        private static GamepadControl.ControlZone? Zone()
        {
            var grid = GridManagerScript.Instance;
            var gamepad = GamepadControl.Instance;
            if (grid == null || gamepad == null || SubmissionScript.Instance == null) return null;
            if (!grid.gameObject.activeInHierarchy) return null;
            return gamepad.currentControlZone;
        }

        public static bool CanInteract => ControlManagerScript.Instance != null && ControlManagerScript.Instance.canInteract;

        // ---- the board ----

        /// <summary>The board's rows, top first, each its cells left to right.</summary>
        public static List<List<Cell>> Board()
        {
            var rows = new List<List<Cell>>();
            var grid = GridManagerScript.Instance;
            if (grid == null || grid.tileHolders == null || grid.tileHolders.Count == 0) return rows;
            var parent = grid.tileHolders[0].transform.parent;
            var layout = parent.GetComponent<GridLayoutGroup>();
            int columns = layout != null && layout.constraint == GridLayoutGroup.Constraint.FixedColumnCount ? layout.constraintCount : 5;

            var holders = new List<TileHolderScript>();
            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (!child.gameObject.activeInHierarchy) continue;
                var holder = child.GetComponent<TileHolderScript>();
                if (holder != null) holders.Add(holder);
            }
            for (int i = 0; i < holders.Count; i++)
            {
                int row = i / columns;
                if (row >= rows.Count) rows.Add(new List<Cell>());
                rows[row].Add(Describe(holders[i], i % columns + 1, row + 1));
            }

            var tools = ToolColumn();
            for (int r = 0; r < rows.Count && r < tools.Count; r++)
            {
                var cell = Describe(tools[r], columns + 1, r + 1);
                rows[r].Add(cell);
            }
            return rows;
        }

        // The tool holders beside the grid, top to bottom.
        private static List<TileHolderScript> ToolColumn()
        {
            var list = new List<TileHolderScript>();
            var refresh = ToolsScript.Instance != null ? ToolsScript.Instance.refresh : null;
            var submit = SubmissionScript.Instance != null ? SubmissionScript.Instance.submissionButton : null;
            var anchor = submit != null ? submit.GetComponentInParent<TileHolderScript>() : null;
            var parent = anchor != null ? anchor.transform.parent : null;
            if (parent == null) return list;
            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (!child.gameObject.activeInHierarchy) continue;
                var holder = child.GetComponent<TileHolderScript>();
                if (holder != null) list.Add(holder);
            }
            return list;
        }

        private static Cell Describe(TileHolderScript holder, int column, int row)
        {
            var cell = new Cell { Column = column, Row = row, Holder = holder };
            var tool = ToolButton(holder);
            if (tool != null)
            {
                cell.Tool = tool;
                cell.Kind = ToolKind(tool);
            }
            else if (holder.bonusSlot && !holder.bonusSlotActive) cell.Kind = CellKind.LockedSlot;
            else if (holder.myTile != null && !holder.myTile.fullySpent) cell.Kind = CellKind.Tile;
            else cell.Kind = CellKind.Empty;
            return cell;
        }

        // A tool sits in its holder's slot as a Button of its own (the holder is a Button too).
        private static Button ToolButton(TileHolderScript holder)
        {
            if (holder.myTile != null) return null;
            foreach (var button in holder.GetComponentsInChildren<Button>(true))
                if (button.gameObject != holder.gameObject && button.GetComponent<TileScript>() == null) return button;
            return null;
        }

        private static CellKind ToolKind(Button button)
        {
            if (SubmissionScript.Instance != null && button == SubmissionScript.Instance.submissionButton) return CellKind.Submit;
            if (button.GetComponent<RefreshHelper>() != null) return CellKind.Refresh;
            var hold = button.GetComponent<LongHoldScript>();
            var target = hold != null ? FirstTarget(hold.onShortPress) : null;
            if (target is BackspaceTool) return CellKind.Backspace;
            if (target is ShuffleTool) return CellKind.Shuffle;
            var click = button.onClick;
            for (int i = 0; i < click.GetPersistentEventCount(); i++)
                if (click.GetPersistentMethodName(i) == "ToggleLetterBagDrawer") return CellKind.Bag;
            return CellKind.Unknown;
        }

        private static UnityEngine.Object FirstTarget(UnityEvent e)
            => e != null && e.GetPersistentEventCount() > 0 ? e.GetPersistentTarget(0) : null;

        /// <summary>"C, 3 points, 4 1": what a cell holds, then its coordinates.</summary>
        public static string CellLine(Cell cell)
        {
            string what;
            switch (cell.Kind)
            {
                case CellKind.Tile:
                    var tile = cell.Tile;
                    what = TileLine(tile);
                    if (tile.tileIsInSubmissionQueue)
                        what = Strings.TileInWord(what, tile.positionOnSubmissionQueue + 1);
                    break;
                case CellKind.LockedSlot: what = Strings.CellLockedSlot; break;
                case CellKind.Empty: what = Strings.CellEmpty; break;
                default: what = ToolLine(cell); break;
            }
            return Strings.CellAt(what, cell.Column, cell.Row);
        }

        /// <summary>A tool's name and state: "Refresh, 5 left", "Submit, disabled".</summary>
        public static string ToolLine(Cell cell)
        {
            var parts = new List<string>();
            var tools = ToolsScript.Instance;
            var logic = LogicManagerScript.Instance;
            switch (cell.Kind)
            {
                case CellKind.Backspace: parts.Add(Strings.ToolBackspace); break;
                case CellKind.Submit: parts.Add(Strings.ToolSubmit); break;
                case CellKind.Refresh:
                    parts.Add(Title(BonusDescriptions.Description.Refresh) ?? Strings.ToolRefresh);
                    if (logic != null) parts.Add(Strings.RefreshesLeft(logic.numberOfRefreshes));
                    break;
                case CellKind.Bag:
                    parts.Add(Title(BonusDescriptions.Description.LetterBag) ?? Strings.ToolBag);
                    if (LetterBagManager.Instance != null) parts.Add(Strings.BagTiles(LetterBagManager.Instance.LetterBag.Count));
                    break;
                case CellKind.Shuffle:
                    bool shuffle = tools == null || tools.shuffle.defaultIsShuffle;
                    parts.Add(Title(shuffle ? BonusDescriptions.Description.Shuffle : BonusDescriptions.Description.Sort)
                        ?? (shuffle ? Strings.ToolShuffle : Strings.ToolSort));
                    break;
                default: parts.Add(cell.Tool != null ? cell.Tool.gameObject.name : Strings.CellEmpty); break;
            }
            if (cell.Tool != null && !cell.Tool.interactable) parts.Add(Strings.StateDisabled);
            return string.Join(", ", parts);
        }

        /// <summary>The game's description of what a cell holds (a special tile's rule, a tool's
        /// help), for the control buffer: the lines after its title.</summary>
        public static IEnumerable<string> CellDetails(Cell cell)
        {
            BonusDescriptions.Description? d = null;
            switch (cell.Kind)
            {
                case CellKind.Tile: d = TileDescription(cell.Tile); break;
                case CellKind.Refresh: d = BonusDescriptions.Description.Refresh; break;
                case CellKind.Bag: d = BonusDescriptions.Description.LetterBag; break;
                case CellKind.Shuffle:
                    d = ToolsScript.Instance == null || ToolsScript.Instance.shuffle.defaultIsShuffle
                        ? BonusDescriptions.Description.Shuffle : BonusDescriptions.Description.Sort;
                    break;
            }
            if (d == null) yield break;
            var lines = DescriptionLines(d.Value);
            // A tool's description opens with its name, already the cell's label.
            int start = cell.Kind == CellKind.Tile ? 0 : 1;
            for (int i = start; i < lines.Count; i++) yield return lines[i];
        }

        /// <summary>The game's description of a tile, for the control buffer (none for a plain one).</summary>
        public static IEnumerable<string> CellDetailsOfTile(TileScript tile)
        {
            var d = TileDescription(tile);
            return d != null ? DescriptionLines(d.Value) : new List<string>();
        }

        // ---- tiles ----

        /// <summary>"C, 3 points", then what makes the tile special: "A, 1 point, emerald",
        /// "E, 1 point, locked, 4 words", "ing, 8 points", "wildcard, 0 points".</summary>
        public static string TileLine(TileScript tile)
        {
            if (tile == null) return Strings.CellEmpty;
            var parts = new List<string> { TileName(tile), Strings.Points(tile.GetTileScore()) };
            switch (tile.myType)
            {
                case SpecialTileManager.TileTypes.Golden: parts.Add(Strings.TileGolden); break;
                case SpecialTileManager.TileTypes.Diamond:
                    parts.Add(tile.mySpecialScore > 0 ? Strings.TileDiamondStored(tile.mySpecialScore) : Strings.TileDiamond);
                    break;
                case SpecialTileManager.TileTypes.Emerald: parts.Add(Strings.TileEmerald); break;
                case SpecialTileManager.TileTypes.Potion: parts.Add(Strings.TilePotion); break;
                case SpecialTileManager.TileTypes.FullStop: parts.Add(Strings.TileDot); break;
                case SpecialTileManager.TileTypes.Transparent: parts.Add(Strings.TileGlass); break;
                case SpecialTileManager.TileTypes.Locked:
                    parts.Add(Strings.TileLocked(tile.mySpecialScore % 10));
                    break;
            }
            if (tile.mandatory) parts.Add(Strings.TileMandatory);
            if (tile.stuckTile) parts.Add(Strings.TileStuck);
            return string.Join(", ", parts);
        }

        /// <summary>The tile's face as a word: its letter ("C", "Qu"), its combination ("ing"), or
        /// what an odd tile is ("wildcard", "plus", "exclamation", "mirror").</summary>
        public static string TileName(TileScript tile)
        {
            if (tile.mirrorTile || tile.myLetter == "2") return Strings.TileMirror;
            switch (tile.myLetter)
            {
                case "*": return Strings.TileWildcard;
                case "+": return Strings.TilePlus;
                case "!": return Strings.TileExclamation;
            }
            string text = tile.GetTileText();
            if (tile.myType == SpecialTileManager.TileTypes.FullStop) text = text.TrimEnd('.');
            return string.IsNullOrEmpty(text) ? tile.myLetter : text;
        }

        private static BonusDescriptions.Description? TileDescription(TileScript tile)
        {
            if (tile == null) return null;
            switch (tile.myType)
            {
                case SpecialTileManager.TileTypes.Golden: return BonusDescriptions.Description.Golden;
                case SpecialTileManager.TileTypes.Diamond: return BonusDescriptions.Description.Diamond;
                case SpecialTileManager.TileTypes.Emerald: return BonusDescriptions.Description.Emerald;
                case SpecialTileManager.TileTypes.Potion: return BonusDescriptions.Description.Potion;
                case SpecialTileManager.TileTypes.FullStop: return BonusDescriptions.Description.Dot;
                case SpecialTileManager.TileTypes.Locked: return BonusDescriptions.Description.Locked;
            }
            if (tile.mirrorTile || tile.myLetter == "2") return BonusDescriptions.Description.Mirror;
            switch (tile.myLetter)
            {
                case "*": return BonusDescriptions.Description.Wildcard;
                case "+": return BonusDescriptions.Description.PlusTile;
                case "!": return BonusDescriptions.Description.Exclamation;
            }
            return null;
        }

        private static List<string> DescriptionLines(BonusDescriptions.Description d)
        {
            var b = BonusDescriptions.Instance;
            return b != null ? GameNodesText.Lines(b.ShopDescription(d)) : new List<string>();
        }

        // A tool description's first line is the tool's name.
        private static string Title(BonusDescriptions.Description d)
        {
            var lines = DescriptionLines(d);
            return lines.Count > 1 ? lines[0] : null;
        }

        // ---- the word ----

        /// <summary>The shown submission slots, left to right (10, or 20 once the tenth is filled).</summary>
        public static List<TileHolderScript> WordSlots()
        {
            var list = new List<TileHolderScript>();
            var sub = SubmissionScript.Instance;
            if (sub == null) return list;
            foreach (var holder in sub.tileHolderScripts)
                if (holder != null && holder.gameObject.activeInHierarchy) list.Add(holder);
            return list;
        }

        /// <summary>The tile in a submission slot, or null.</summary>
        public static TileScript SlotTile(TileHolderScript slot)
        {
            var sub = SubmissionScript.Instance;
            int id = slot.submissionID;
            return sub != null && id >= 0 && id < sub.submittingTiles.Count ? sub.submittingTiles[id] : null;
        }

        /// <summary>"C, 3 points, slot 3", or "slot 5, empty, +5" (with "required" for slots 1 to 4).</summary>
        public static string SlotLine(TileHolderScript slot)
        {
            int n = slot.submissionID + 1;
            var tile = SlotTile(slot);
            if (tile != null) return Strings.SlotWithTile(TileLine(tile), n);
            var parts = new List<string> { Strings.Slot(n), Strings.CellEmpty };
            if (n <= RequiredTiles()) parts.Add(Strings.SlotRequired);
            string bonus = SlotBonus(slot);
            if (bonus != null) parts.Add(bonus);
            return string.Join(", ", parts);
        }

        // The slot's "+5" as the game draws it.
        private static string SlotBonus(TileHolderScript slot)
        {
            foreach (var text in slot.GetComponentsInChildren<TMP_Text>(false))
                if (text.gameObject.name == "Submission Number" && !string.IsNullOrWhiteSpace(text.text)) return text.text.Trim();
            return null;
        }

        private static int RequiredTiles()
        {
            var sub = SubmissionScript.Instance;
            int n = sub != null ? Private.Get<int>(sub, "numberOfTilesRequired") : 0;
            return n > 0 ? n : 4;
        }

        /// <summary>The word so far as one line: the tiles' names in slot order, gaps skipped.</summary>
        public static string WordSoFar()
        {
            var sub = SubmissionScript.Instance;
            if (sub == null) return "";
            var names = new List<string>();
            foreach (var tile in sub.submittingTiles)
                if (tile != null) names.Add(TileName(tile));
            return string.Join(" ", names);
        }

        // ---- status ----

        private static BarScript ScoreBar() => LogicManagerScript.Instance != null ? Private.Get<BarScript>(LogicManagerScript.Instance, "scoreBarScript") : null;
        private static LifeBarScript LifeBar() => LogicManagerScript.Instance != null ? Private.Get<LifeBarScript>(LogicManagerScript.Instance, "lifeBarScript") : null;

        private static string Shown(TMP_Text text)
            => text != null && text.gameObject.activeInHierarchy && !string.IsNullOrWhiteSpace(text.text) ? text.text : null;

        private static string Join(params string[] parts)
        {
            var kept = new List<string>();
            foreach (var p in parts) if (!string.IsNullOrWhiteSpace(p)) kept.Add(p.Trim());
            return kept.Count > 0 ? string.Join(", ", kept) : null;
        }

        /// <summary>"0 / 35, 35 Points Left", as the score bar draws it.</summary>
        public static string ScoreLine()
        {
            var bar = ScoreBar();
            return bar == null ? null : Join(Shown(Private.Get<TMP_Text>(bar, "barLabel")), Shown(Private.Get<TMP_Text>(bar, "scoreToGoTMP")));
        }

        /// <summary>"ROUND 1 / 10, EASY MODE".</summary>
        public static string RoundLine()
        {
            var bar = ScoreBar();
            return bar == null ? null : Join(Shown(Private.Get<TMP_Text>(bar, "roundText")), Shown(Private.Get<TMP_Text>(bar, "leftText")));
        }

        /// <summary>"PLAYS: 10", the plays bar's label and number.</summary>
        public static string PlaysLine()
        {
            var bar = LifeBar();
            if (bar == null) return null;
            string label = Shown(Private.Get<TMP_Text>(bar, "playsLabel"));
            string value = Shown(Private.Get<TMP_Text>(bar, "barLabel"));
            return value == null ? null : Strings.StatusPair(label ?? Strings.StatusPlays, value);
        }

        public static string RefreshesLine()
        {
            var logic = LogicManagerScript.Instance;
            return logic == null ? null : Strings.StatusPair(Title(BonusDescriptions.Description.Refresh) ?? Strings.ToolRefresh, logic.numberOfRefreshes.ToString());
        }

        public static string BagLine()
        {
            var bag = LetterBagManager.Instance;
            return bag == null ? null : Strings.StatusPair(Title(BonusDescriptions.Description.LetterBag) ?? Strings.ToolBag, bag.LetterBag.Count.ToString());
        }

        /// <summary>The strip above the word while it shows something: the last word and its score,
        /// a special round's rule, or a passing message.</summary>
        public static string MessageLine()
        {
            var area = LastWordArea.Instance;
            if (area == null || !Private.Get<bool>(area, "showing")) return null;
            return Shown(Private.Get<TMP_Text>(area, "lastWordLandscape"));
        }
    }

    /// <summary>Line splitting shared by the round's readers: the game's text split at its line
    /// breaks, markup stripped, blanks dropped.</summary>
    internal static class GameNodesText
    {
        public static List<string> Lines(string text)
        {
            var lines = new List<string>();
            if (string.IsNullOrEmpty(text)) return lines;
            foreach (var line in text.Split('\n'))
            {
                string clean = Earplay.Contracts.TextFilter.Clean(line);
                if (clean.Length > 0) lines.Add(clean);
            }
            return lines;
        }
    }
}
