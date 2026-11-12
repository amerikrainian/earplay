using System.Collections.Generic;
using Earplay.Core;
using Earplay.Core.Graph;
using Earplay.Core.Input;
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
    /// The round: two stops. The board, with the word strip as its last row, lined up under the board's
    /// columns (slot n under column n; Up from slot 7 and beyond reaches column 6; Alt+W jumps to the
    /// strip keeping the column), and the status. While it has focus it lends the
    /// keyboard to the game (<see cref="IKeyboardLender"/>), so a letter typed anywhere on it is the
    /// game's typing (the game picks the tile and sends it to the word), and the game's own digits,
    /// drawer keys, Shift, 8 and = work as in the game; the game's actions that clash with ours stay
    /// off: its arrows and Space (a controller cursor we replace), Tab (close gaps: Ctrl+Backspace
    /// here), Enter (submit: Enter on the word stop or the Submit tool here), Backspace (ours: the last
    /// tile out; Shift+Backspace the whole word), Escape (ours: the Options drawer, the same toggle).
    /// The game's tool keys are ours too, calling the game's own methods, so the key help lists them
    /// and runs them: 2 the shuffle tool's press (Shuffle or Sort, the player's default), Shift+2 the
    /// other (the game's hold on 2), 3 Refresh, held a second as the game wants it (a short tap gets
    /// the game's own "Press and Hold to Refresh"; run from the key help, where nothing is held, it
    /// refreshes at once), 8 and = the wildcard and plus tiles, offered while the board has one.
    /// No type-ahead: letters are the game's.
    /// </summary>
    public sealed class RoundScreen : CompositeScreen, IKeyboardLender
    {
        public const string ClearAction = "round.clear";
        public const string CloseGapsAction = "round.closegaps";
        public const string ShuffleAction = "round.shuffle";
        public const string ShuffleOtherAction = "round.shuffle.other";
        public const string RefreshAction = "round.refresh";
        public const string WildcardAction = "round.wildcard";
        public const string PlusAction = "round.plus";
        public const string WordAction = "round.word";
        public const string SubmitAction = "round.submit";

        private const float RefreshHold = 1f;

        private static readonly string[] Blocked =
        {
            "Navigate", "Interact", "Nav Left", "Nav Right", "Close Gaps", "Submit Tiles", "Backspace", "Options",
            "Shuffle Tiles", "Refresh Tiles", "Type Wildcard", "Insert Plus",
        };

        // When the Refresh key went down (unscaled seconds), while it is held towards a refresh.
        private float? _refreshSince;

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
            yield return new ElementAction(WordAction, Strings.Get("bind.round.word"), _ => JumpToWord());
            yield return new ElementAction(SubmitAction, Strings.Get("bind.round.submit"), _ => Submit());
            bool shuffleFirst = ToolsScript.Instance == null || ToolsScript.Instance.shuffle.defaultIsShuffle;
            yield return new ElementAction(ShuffleAction, shuffleFirst ? Strings.ToolShuffle : Strings.ToolSort, _ =>
            {
                if (ToolsScript.Instance != null && Round.CanInteract) ToolsScript.Instance.shuffle.ShuffleButtonOnPress();
            });
            yield return new ElementAction(ShuffleOtherAction, shuffleFirst ? Strings.ToolSort : Strings.ToolShuffle, _ =>
            {
                if (ToolsScript.Instance != null && Round.CanInteract) ToolsScript.Instance.shuffle.ShuffleButtonOnHold();
            });
            yield return new ElementAction(RefreshAction, Strings.Get("bind.round.refresh"), _ => StartRefresh());
            if (BoardHas("*")) yield return new ElementAction(WildcardAction, Strings.TileWildcard, _ => SendFromBoard("*"));
            if (BoardHas("+")) yield return new ElementAction(PlusAction, Strings.TilePlus, _ => SendFromBoard("+"));
            yield return new ElementAction(CloseGapsAction, Strings.Get("bind.round.closegaps"), _ =>
            {
                if (!Round.CanInteract || SubmissionScript.Instance == null) return;
                SubmissionScript.Instance.CloseAllGaps();
                Later.Frames(20, () => Speech.Say(Strings.GapsClosed(Round.WordSoFar()), interrupt: true), "close gaps");
            });
        }

        // Alt+W: the word strip, keeping the column: from board column n, slot n; from the status, or
        // a column the strip does not reach, slot 1. Landed on as a move (interrupting).
        private static void JumpToWord()
        {
            int slot = 0;
            var key = Navigation.FocusedNode?.Id?.StructuralKey as string;
            if (key != null && key.StartsWith("board:", System.StringComparison.Ordinal))
            {
                var parts = key.Split(':');
                if (parts.Length == 3 && int.TryParse(parts[1], out int column)) slot = column - 1;
            }
            if (slot >= Round.WordSlots().Count) slot = 0;
            if (key != null && key.StartsWith("word:", System.StringComparison.Ordinal)) return; // already there
            Navigation.MoveTo(ControlId.Structural("word:" + slot));
        }

        // A real key press holds a second before refreshing, as the game's own key does; a press with
        // no key down (the key help running it) refreshes at once.
        private void StartRefresh()
        {
            var action = InputManager.Find(RefreshAction);
            if (action != null && action.Held) _refreshSince = Time.unscaledTime;
            else Refresh();
        }

        private static void Refresh()
        {
            if (ToolsScript.Instance != null) ToolsScript.Instance.refresh.PressedRefreshButton();
        }

        public override void OnUpdate()
        {
            base.OnUpdate();
            if (_refreshSince == null) return;
            var action = InputManager.Find(RefreshAction);
            if (action != null && action.Held)
            {
                if (Time.unscaledTime - _refreshSince.Value < RefreshHold) return;
                _refreshSince = null;
                Refresh();
                return;
            }
            // Let go too soon: the game's own refusal, through its message strip (which speaks it).
            _refreshSince = null;
            if (LastWordArea.Instance != null)
                LastWordArea.Instance.TutorialMessage("Press and Hold to Refresh", autoHide: true, overrideSpecialRound: false, overrideLastWord: false);
        }

        // Whether the board has a tile of this letter that is free to go into the word.
        private static bool BoardHas(string letter) => FreeTile(letter) != null;

        private static TileScript FreeTile(string letter)
        {
            var grid = GridManagerScript.Instance;
            if (grid == null) return null;
            foreach (var holder in grid.tileHolders)
            {
                var tile = holder != null ? holder.myTile : null;
                if (tile != null && !tile.tileIsInSubmissionQueue && !tile.fullySpent
                    && tile.myType != SpecialTileManager.TileTypes.Locked && tile.myLetter == letter) return tile;
            }
            return null;
        }

        // The game's 8 and = keys: the first such tile to the word.
        private static void SendFromBoard(string letter)
        {
            if (!Round.CanInteract) return;
            var tile = FreeTile(letter);
            if (tile == null) return;
            tile.SendTileToSubmission();
            if (SubmissionScript.Instance != null) SubmissionScript.Instance.TriggerSubmitSound();
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
        // Read-only (the shop's peek): every cell reads, nothing acts.
        private readonly bool _readOnly;
        public BoardSection(bool readOnly = false) { _readOnly = readOnly; }

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
                        OnActivate = _readOnly ? null : Activator(c),
                        OnSecondary = _readOnly ? null : (System.Action)RoundScreen.RemoveLast,
                        OnDrag = !_readOnly && c.Kind == CellKind.Tile ? () => { var now = Fresh(c); TileSwap.Press(now.Holder, now.Tile); } : (System.Action)null,
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

    /// <summary>The word: the shown submission slots, the board stop's last row, sharing the board's
    /// row key so slot n sits under column n. Enter submits from any slot, Delete sends the slot's tile
    /// back to the board, Backspace takes the last tile out.</summary>
    internal sealed class WordSection : ScreenSection
    {
        private readonly bool _readOnly;
        public WordSection(bool readOnly = false) { _readOnly = readOnly; }

        public override void Build(GraphBuilder b)
        {
            var slots = Round.WordSlots();
            if (slots.Count == 0) return;
            b.PushContext(Strings.RoundWord);
            b.StartRow("board");
            foreach (var slot in slots)
            {
                var s = slot;
                b.AddItem(ControlId.Structural("word:" + s.submissionID), new NodeVtable
                {
                    ControlType = ControlTypes.Text,
                    Announcements = new[] { GameNodes.LabelPart(() => Round.SlotLine(s)) },
                    Details = () => Round.SlotTile(s) != null ? Round.CellDetailsOfTile(Round.SlotTile(s)) : null,
                    OnActivate = _readOnly ? null : (System.Action)RoundScreen.Submit,
                    OnSecondary = _readOnly ? null : (System.Action)RoundScreen.RemoveLast,
                    OnDelete = _readOnly ? null : (System.Action)(() => { if (Round.SlotTile(s) != null && Round.CanInteract) s.ControllerInteractTile(); }),
                    OnDrag = _readOnly ? null : (System.Action)(() => TileSwap.Press(s, Round.SlotTile(s))),
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
        private readonly bool _readOnly;
        public StatusSection(bool readOnly = false) { _readOnly = readOnly; }

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

        private void Line(GraphBuilder b, string key, System.Func<string> text)
        {
            if (text() == null) return;
            var vt = GameNodes.Text(text);
            if (!_readOnly) vt.OnSecondary = RoundScreen.RemoveLast;
            b.AddItem(ControlId.Structural("status:" + key), vt);
        }
    }
}
