using System;
using System.Collections.Generic;
using Earplay.Core;
using Earplay.Core.Strings;
using Earplay.Module.Interop;
using Earplay.Module.UI;
using HarmonyLib;

namespace Earplay.Module.Round
{
    /// <summary>
    /// What the round says as it happens. The word row is watched every frame while the round takes
    /// input: a tile arriving is "C" (typed or pressed alike, interrupting, so fast typing stays
    /// current; the game puts it in the first empty slot), one leaving is "C removed". Everything else is the game's own calls, hooked:
    /// <list type="bullet">
    /// <item><c>KeyboardControl.Update</c> handles one typed letter a frame; when it found no tile for
    /// it (the row did not change), "no C".</item>
    /// <item><c>SubmitWord.SubmitTheWord</c> starts a word (the board is noted, to name the tiles dealt
    /// into it afterwards); <c>WordWasNotAccepted</c> is "SMOE: not a word"; <c>ReturnToGame</c> (play
    /// goes on) and <c>LogicManagerScript.FinishRound</c> (the round is won) end it with the game's
    /// "WORD: 24", the score line, and the new tiles with their coordinates.</item>
    /// <item><c>LastWordArea.TutorialMessage</c> is the strip's passing message ("Words Must Have At
    /// Least 4 Tiles", "Press and Hold to Refresh", "Did you mean: X?"); <c>ShowSpecialRoundText</c> a
    /// special round's rule.</item>
    /// <item><c>ShuffleTool.ShuffleTiles</c> / <c>SortTilesAlphabetically</c> and
    /// <c>RefreshTool.RefreshAllTiles</c> (the player's own) say what happened to the board.</item>
    /// </list>
    /// While a word is being scored (interaction paused) the row's changes are the game's, not the
    /// player's, and are not read.
    /// </summary>
    internal static class RoundEvents
    {
        private static readonly List<TileScript> _row = new List<TileScript>();
        private static bool _submitting;
        private static bool _expectClear;
        private static Dictionary<TileHolderScript, string> _boardBefore;
        private static int _quietUntil = int.MinValue / 2;

        /// <summary>The word row's announcements stand down for a few frames (a keyboard move speaks
        /// its own result).</summary>
        public static void QuietFor(int frames) => _quietUntil = UnityEngine.Time.frameCount + frames;

        /// <summary>The next mass removal is the player's own clear, already spoken.</summary>
        public static void ExpectClear() => _expectClear = true;

        public static void Tick()
        {
            var sub = SubmissionScript.Instance;
            if (sub == null) { _row.Clear(); return; }
            bool announce = Round.Playing && Round.CanInteract && !_submitting
                && UnityEngine.Time.frameCount >= _quietUntil;
            var now = sub.submittingTiles;
            if (announce && _row.Count == now.Count)
            {
                var added = new List<int>();
                var removed = new List<TileScript>();
                for (int i = 0; i < now.Count; i++)
                {
                    if (ReferenceEquals(now[i], _row[i])) continue;
                    if (now[i] != null && !_row.Contains(now[i])) added.Add(i);
                    if (_row[i] != null && !now.Contains(_row[i])) removed.Add(_row[i]);
                }
                foreach (int i in added)
                    Speech.Say(Round.TileName(now[i]), interrupt: true);
                if (removed.Count == 1 && added.Count == 0)
                    Speech.Say(Strings.TileRemoved(Round.TileName(removed[0])), interrupt: true);
            }
            if (_expectClear && sub.numberOfFilledTiles == 0) _expectClear = false;
            _row.Clear();
            _row.AddRange(now);
        }

        public static void Reset()
        {
            _row.Clear();
            _submitting = false;
            _expectClear = false;
            _boardBefore = null;
        }

        // The board's tiles by holder, to name the ones a word's refill dealt.
        private static Dictionary<TileHolderScript, string> BoardSnapshot()
        {
            var map = new Dictionary<TileHolderScript, string>();
            foreach (var row in Round.Board())
                foreach (var cell in row)
                    if (cell.Kind == CellKind.Tile) map[cell.Holder] = Key(cell.Tile);
            return map;
        }

        private static string Key(TileScript t) => t == null ? "" : t.GetInstanceID() + ":" + t.myLetter + ":" + t.myType;

        private static string NewTiles(Dictionary<TileHolderScript, string> before)
        {
            if (before == null) return null;
            var parts = new List<string>();
            foreach (var row in Round.Board())
                foreach (var cell in row)
                {
                    if (cell.Kind != CellKind.Tile || cell.Tile.tileIsInSubmissionQueue) continue;
                    if (before.TryGetValue(cell.Holder, out var was) && was == Key(cell.Tile)) continue;
                    string name = Round.TileName(cell.Tile);
                    parts.Add(ModuleSettings.SpeakCoordinates ? Strings.CellAt(name, cell.Column, cell.Row) : name);
                }
            return parts.Count > 0 ? Strings.NewTiles(string.Join("; ", parts)) : null;
        }

        // The word's result, once the game has dealt the new tiles and redrawn the bar.
        private static void WordDone(bool roundWon)
        {
            var before = _boardBefore;
            _boardBefore = null;
            Later.Frames(15, () =>
            {
                _submitting = false;
                var area = LastWordArea.Instance;
                string word = area != null ? Private.Get<string>(area, "lastWord") : null;
                int score = area != null ? Private.Get<int>(area, "lastScore") : 0;
                if (!string.IsNullOrEmpty(word)) Speech.Say(Strings.WordScored(word, score));
                if (roundWon) Speech.Say(Strings.RoundComplete);
                string line = Round.ScoreLine();
                if (line != null) Speech.Say(line);
                if (!roundWon)
                {
                    string fresh = NewTiles(before);
                    if (fresh != null) Speech.Say(fresh);
                }
            }, "word result");
        }

        // ---- hooks ----

        [HarmonyPatch(typeof(KeyboardControl), "Update")]
        private static class TypedLetter
        {
            private static void Prefix(KeyboardControl __instance, out string __state)
            {
                __state = null;
                var buffer = Private.Get<Queue<string>>(__instance, "keyBuffer");
                if (buffer != null && buffer.Count > 0)
                {
                    int filled = SubmissionScript.Instance != null ? SubmissionScript.Instance.numberOfFilledTiles : 0;
                    __state = buffer.Peek() + "|" + filled;
                }
            }

            private static void Postfix(string __state)
            {
                if (__state == null || SubmissionScript.Instance == null) return;
                try
                {
                    int bar = __state.IndexOf('|');
                    string letter = __state.Substring(0, bar);
                    int before = int.Parse(__state.Substring(bar + 1));
                    // A found tile is on its way into the row (counted on arrival), so look a few
                    // frames on.
                    Later.Frames(3, () =>
                    {
                        var sub = SubmissionScript.Instance;
                        if (sub == null || sub.numberOfFilledTiles != before) return;
                        foreach (var t in sub.submittingTiles) if (t != null && t.tileIsMoving) return;
                        Speech.Say(Strings.NoTile(letter), interrupt: true);
                    }, "no tile");
                }
                catch (Exception e) { CoreLog.Warning("RoundEvents: typed letter: " + e.Message); }
            }
        }

        [HarmonyPatch(typeof(SubmitWord), nameof(SubmitWord.SubmitTheWord))]
        private static class WordStarted
        {
            private static void Prefix()
            {
                _submitting = true;
                _boardBefore = BoardSnapshot();
                TileSwap.Reset();
            }
        }

        [HarmonyPatch(typeof(SubmitWord), "WordWasNotAccepted")]
        private static class WordRejected
        {
            private static void Postfix(string wrongWord)
            {
                _submitting = false;
                _boardBefore = null;
                string word = !string.IsNullOrEmpty(wrongWord) ? wrongWord : Round.WordSoFar();
                Speech.Say(Strings.WordRejected(word));
            }
        }

        [HarmonyPatch(typeof(SubmitWord), "ReturnToGame")]
        private static class WordScored
        {
            private static void Postfix() => WordDone(roundWon: false);
        }

        [HarmonyPatch(typeof(LogicManagerScript), nameof(LogicManagerScript.FinishRound))]
        private static class RoundFinished
        {
            private static void Prefix() => WordDone(roundWon: true);
        }

        [HarmonyPatch(typeof(LastWordArea), nameof(LastWordArea.TutorialMessage))]
        private static class StripMessage
        {
            private static void Postfix(string message)
            {
                if (!string.IsNullOrWhiteSpace(message)) Speech.Say(message);
            }
        }

        [HarmonyPatch(typeof(LastWordArea), nameof(LastWordArea.ShowSpecialRoundText))]
        private static class SpecialRound
        {
            private static void Postfix()
            {
                Later.Frames(2, () =>
                {
                    string line = Round.MessageLine();
                    if (line != null) Speech.Say(line);
                }, "special round");
            }
        }

        [HarmonyPatch(typeof(ShuffleTool), nameof(ShuffleTool.ShuffleTiles))]
        private static class Shuffled
        {
            private static void Prefix(out Dictionary<TileHolderScript, string> __state) => __state = BoardSnapshot();
            private static void Postfix(Dictionary<TileHolderScript, string> __state) => SayIfMoved(__state, Strings.Shuffled);
        }

        [HarmonyPatch(typeof(ShuffleTool), nameof(ShuffleTool.SortTilesAlphabetically))]
        private static class Sorted
        {
            private static void Prefix(out Dictionary<TileHolderScript, string> __state) => __state = BoardSnapshot();
            private static void Postfix(Dictionary<TileHolderScript, string> __state) => SayIfMoved(__state, Strings.Sorted);
        }

        // Shuffle and sort refuse silently while interaction is paused: speak only when tiles moved.
        // The tiles change holders over a short animation, so look once it has settled.
        private static void SayIfMoved(Dictionary<TileHolderScript, string> before, string line)
        {
            Later.Frames(20, () =>
            {
                var after = BoardSnapshot();
                foreach (var kv in after)
                    if (!before.TryGetValue(kv.Key, out var was) || was != kv.Value) { Speech.Say(line, interrupt: true); return; }
            }, "shuffle");
        }

        [HarmonyPatch(typeof(RefreshTool), nameof(RefreshTool.RefreshAllTiles))]
        private static class Refreshed
        {
            private static void Postfix(bool playerActivatedRefresh)
            {
                if (!playerActivatedRefresh || LogicManagerScript.Instance == null) return;
                Speech.Say(Strings.Refreshed(LogicManagerScript.Instance.numberOfRefreshes), interrupt: true);
            }
        }
    }
}
