using System.Collections.Generic;
using Earplay.Core;
using Earplay.Core.Graph;
using Earplay.Core.Screens;
using Earplay.Core.Strings;
using Earplay.Core.UI;
using Earplay.Module.UI;
using UnityEngine;
using Navigation = Earplay.Core.UI.Navigation;
using Screen = Earplay.Core.Screens.Screen;

namespace Earplay.Module.Round
{
    /// <summary>
    /// The round: three stops, the board, the word, the status. While it has focus it lends the
    /// keyboard to the game (<see cref="IKeyboardLender"/>), so a letter typed anywhere on it is the
    /// game's typing (the game picks the tile and sends it to the word), and the game's own digits,
    /// drawer keys, Shift, 8 and = work as in the game; the game's actions that clash with ours stay
    /// off: its arrows and Space (a controller cursor we replace), Tab (close gaps: Ctrl+Backspace
    /// here), Enter (submit: Enter on the word stop or the Submit tool here), Backspace (ours: the last
    /// tile out; Shift+Backspace the whole word), Escape (ours: the Options drawer, the same toggle).
    /// No type-ahead: letters are the game's.
    /// </summary>
    public sealed class RoundScreen : CompositeScreen, IKeyboardLender
    {
        public const string ClearAction = "round.clear";
        public const string CloseGapsAction = "round.closegaps";

        private static readonly string[] Blocked =
        {
            "Navigate", "Interact", "Nav Left", "Nav Right", "Close Gaps", "Submit Tiles", "Backspace", "Options",
        };

        public RoundScreen()
        {
            Add(new BoardSection());
            Add(new WordSection());
            Add(new StatusSection());
        }

        public override string Key => "game.round";
        public override bool AllowsTypeahead => false;
        public IReadOnlyList<string> BlockedGameActions => Blocked;

        public override bool IsActive() => Round.OnScreen;
        // Focus survives the shop and the end of a round: back on the board where it was.
        public override bool KeepStateOnPop => true;

        public override void OnPop()
        {
            base.OnPop();
            TileSwap.Reset();
        }

        protected override IEnumerable<ElementAction> OwnActions()
        {
            // Escape puts a held tile down; with none held it opens the Options drawer.
            yield return new ElementAction(ActionIds.Back, TileSwap.Holding ? Strings.Get("bind.round.putdown") : Strings.TitleOptions, _ =>
            {
                if (TileSwap.Holding) { TileSwap.Cancel(); return; }
                if (Round.CanInteract && TopDrawerManager.Instance != null) TopDrawerManager.Instance.ToggleOptionsDrawer();
            });
            yield return new ElementAction(ClearAction, Strings.Get("bind.round.clear"), _ => ClearWord());
            yield return new ElementAction(CloseGapsAction, Strings.Get("bind.round.closegaps"), _ =>
            {
                if (!Round.CanInteract || SubmissionScript.Instance == null) return;
                SubmissionScript.Instance.CloseAllGaps();
                Later.Frames(20, () => Speech.Say(Strings.GapsClosed(Round.WordSoFar()), interrupt: true), "close gaps");
            });
        }

        /// <summary>The last tile added goes back to the board (the game's Backspace tap).</summary>
        internal static void RemoveLast()
        {
            var tools = ToolsScript.Instance;
            if (tools != null) tools.backspace.BackspaceButtonClick();
        }

        // The game's Backspace hold: every tile back to the board.
        private static void ClearWord()
        {
            var tools = ToolsScript.Instance;
            if (tools == null || SubmissionScript.Instance == null || SubmissionScript.Instance.numberOfFilledTiles == 0) return;
            RoundEvents.ExpectClear();
            tools.backspace.BackspaceButtonHold();
            Speech.Say(Strings.WordCleared, interrupt: true);
        }

        /// <summary>The game's Submit press: its own refusals ("Words Must Have At Least 4 Tiles")
        /// come back through the message strip.</summary>
        internal static void Submit()
        {
            if (SubmissionScript.Instance != null) SubmissionScript.Instance.PressedSubmitButton();
        }
    }

    /// <summary>The board as drawn: rows top first, one shared row key so Up/Down keep the column,
    /// each cell its contents then its coordinates.</summary>
    internal sealed class BoardSection : ScreenSection
    {
        public override void Build(GraphBuilder b)
        {
            var rows = Round.Board();
            if (rows.Count == 0) return;
            b.BeginStop("board");
            // A first visit lands on the first letter tile, not a locked bonus slot.
            foreach (var row in rows)
            {
                var first = row.Find(c => c.Kind == CellKind.Tile);
                if (first != null) { b.SetStart(ControlId.Structural("board:" + first.Column + ":" + first.Row)); break; }
            }
            b.PushContext(Strings.RoundBoard);
            foreach (var row in rows)
            {
                b.StartRow("board");
                foreach (var cell in row)
                {
                    var c = cell;
                    b.AddItem(ControlId.Structural("board:" + c.Column + ":" + c.Row), new NodeVtable
                    {
                        ControlType = ControlTypes.Text,
                        Announcements = new[] { GameNodes.LabelPart(() => Round.CellLine(Fresh(c))) },
                        Details = () => Round.CellDetails(Fresh(c)),
                        OnActivate = Activator(c),
                        OnSecondary = RoundScreen.RemoveLast,
                        OnDrag = c.Kind == CellKind.Tile ? () => { var now = Fresh(c); TileSwap.Press(now.Holder, now.Tile); } : (System.Action)null,
                        ExcludeFromSearch = true,
                    });
                }
                b.EndRow();
            }
            b.PopContext();
        }

        // The cell as it is now: a tile can leave or arrive between the render and the key.
        private static Cell Fresh(Cell c)
        {
            var rows = Round.Board();
            return c.Row - 1 < rows.Count && c.Column - 1 < rows[c.Row - 1].Count ? rows[c.Row - 1][c.Column - 1] : c;
        }

        private static System.Action Activator(Cell c)
        {
            switch (c.Kind)
            {
                case CellKind.Tile:
                    return () => { var now = Fresh(c); if (now.Holder != null) now.Holder.ControllerInteractTile(); };
                case CellKind.Backspace: return RoundScreen.RemoveLast;
                case CellKind.Submit: return RoundScreen.Submit;
                case CellKind.Refresh:
                    return () => { if (ToolsScript.Instance != null) ToolsScript.Instance.refresh.PressedRefreshButton(); };
                case CellKind.Shuffle:
                    return () => { if (ToolsScript.Instance != null && Round.CanInteract) ToolsScript.Instance.shuffle.ShuffleButtonOnPress(); };
                case CellKind.Bag:
                    return () => BagScreen.Open();
                default: return null;
            }
        }
    }

    /// <summary>The word: the shown submission slots in a row. Enter submits from any slot, Delete
    /// sends the slot's tile back to the board, Backspace takes the last tile out.</summary>
    internal sealed class WordSection : ScreenSection
    {
        public override void Build(GraphBuilder b)
        {
            var slots = Round.WordSlots();
            if (slots.Count == 0) return;
            b.BeginStop("word");
            b.PushContext(Strings.RoundWord);
            b.StartRow();
            foreach (var slot in slots)
            {
                var s = slot;
                b.AddItem(ControlId.Structural("word:" + s.submissionID), new NodeVtable
                {
                    ControlType = ControlTypes.Text,
                    Announcements = new[] { GameNodes.LabelPart(() => Round.SlotLine(s)) },
                    Details = () => Round.SlotTile(s) != null ? Round.CellDetailsOfTile(Round.SlotTile(s)) : null,
                    OnActivate = RoundScreen.Submit,
                    OnSecondary = RoundScreen.RemoveLast,
                    OnDelete = () => { if (Round.SlotTile(s) != null && Round.CanInteract) s.ControllerInteractTile(); },
                    OnDrag = () => TileSwap.Press(s, Round.SlotTile(s)),
                    ExcludeFromSearch = true,
                });
            }
            b.EndRow();
            b.PopContext();
        }
    }

    /// <summary>The status lines, each read live: score, round, plays, refreshes, the bag, and the
    /// message strip while it shows something.</summary>
    internal sealed class StatusSection : ScreenSection
    {
        public override void Build(GraphBuilder b)
        {
            b.BeginStop("status");
            b.PushContext(Strings.RoundStatus);
            Line(b, "score", Round.ScoreLine);
            Line(b, "round", Round.RoundLine);
            Line(b, "plays", Round.PlaysLine);
            Line(b, "refreshes", Round.RefreshesLine);
            Line(b, "bag", Round.BagLine);
            if (Round.MessageLine() != null) Line(b, "message", Round.MessageLine);
            b.PopContext();
        }

        private static void Line(GraphBuilder b, string key, System.Func<string> text)
        {
            if (text() == null) return;
            var vt = GameNodes.Text(text);
            vt.OnSecondary = RoundScreen.RemoveLast;
            b.AddItem(ControlId.Structural("status:" + key), vt);
        }
    }
}
