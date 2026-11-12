using System.Collections.Generic;
using Earplay.Core;
using Earplay.Core.Strings;
using Earplay.Module.Interop;
using HarmonyLib;
using TMPro;

namespace Earplay.Module.Round
{
    /// <summary>
    /// A word's scoring as the player watches it (<c>SubmitWord.WordIsCorrect</c>), each step spoken as
    /// the game shows it, queued in order; off in the mod menu ("Score breakdown"), only the total is
    /// read, as before. The steps are the game's own display calls, hooked:
    /// <list type="bullet">
    /// <item>a tile's points landing on its slot (<c>SubmissionTileSpace.showScore</c>): "B, 3";</item>
    /// <item>an emerald's hit (<c>SpecialTileManager.PingEmerald</c>), each golden tile counted
    /// (<c>PlayGoldSubmissionSound</c>), the dot's multiplier (<c>PingDot</c>) and the golden one
    /// (<c>ScoreZoneScript.FinishGold</c>);</item>
    /// <item>a modifier taking effect (<c>ModifierStubScript.PingModifierStub</c>, the stub's flash):
    /// its stub text;</item>
    /// <item>the total, letters plus bonus (<c>BarScript.ChangeFinalScore</c>, the parts from the
    /// submitter's <c>wordScoreForLog</c> / <c>bonusScoreForLog</c>), and a modifier's change to it
    /// (<c>ChangeFinalFinalScore</c>);</item>
    /// <item>the plays bar's popup (<c>LifeBarScript.PopupText</c>: "-1", a potion's "+3"), while the
    /// round is on.</item>
    /// </list>
    /// A modifier's flash outside a word (a free refresh, the start of a round) is read the same way.
    /// <para>The flashes of the moment a word is accepted (each modifier's <c>OnCorrectWordSubmitted</c>
    /// and <c>LivesToLose</c>, before the tiles score: from <c>SubmitTheWord</c> to
    /// <c>BarScript.FadeInScore</c>) are held back: many only notice the word ("5 Tiles, First and
    /// Last 3x" counts it and acts later, on its tiles). A held flash is dropped when the same modifier
    /// flashes again while the word scores (its effect is announced where it happens), and spoken
    /// just before the total otherwise (some act at that moment alone: points added to every tile, a
    /// counter advanced).</para>
    /// </summary>
    internal static class ScoreBreakdown
    {
        private static int _golden;

        // Where a word's scoring is: before the tiles score (flashes held), while they and the word
        // score, or no word at all.
        private enum Phase { Idle, Accepted, Scoring }
        private static Phase _phase;
        private static readonly List<ModifierStubScript> _held = new List<ModifierStubScript>();

        private static string StubText(ModifierStubScript stub)
            => ModifiersSection.Text(Private.Get<TMP_Text>(stub, "myDescription"));

        // The held flashes no later one repeated, said before the total.
        private static void SayHeld()
        {
            foreach (var stub in _held)
                if (stub != null) Say(StubText(stub));
            _held.Clear();
        }

        [HarmonyPatch(typeof(SubmitWord), nameof(SubmitWord.SubmitTheWord))]
        private static class WordStarted
        {
            private static void Prefix()
            {
                _phase = Phase.Accepted;
                _held.Clear();
            }
        }

        [HarmonyPatch(typeof(SubmitWord), "WordWasNotAccepted")]
        private static class WordRefused
        {
            private static void Prefix()
            {
                _phase = Phase.Idle;
                _held.Clear();
            }
        }

        [HarmonyPatch(typeof(BarScript), nameof(BarScript.FadeInScore))]
        private static class TilesScoring
        {
            private static void Postfix() { if (_phase == Phase.Accepted) _phase = Phase.Scoring; }
        }

        [HarmonyPatch(typeof(SubmitWord), "ReturnToGame")]
        private static class WordDoneReturned
        {
            private static void Postfix() { SayHeld(); _phase = Phase.Idle; }
        }

        [HarmonyPatch(typeof(LogicManagerScript), nameof(LogicManagerScript.FinishRound))]
        private static class WordDoneRoundWon
        {
            private static void Prefix() { SayHeld(); _phase = Phase.Idle; }
        }

        private static bool On => ModuleSettings.ScoreBreakdown;

        private static void Say(string line)
        {
            if (On && !string.IsNullOrWhiteSpace(line)) Speech.Say(line);
        }

        [HarmonyPatch(typeof(SubmissionTileSpace), nameof(SubmissionTileSpace.showScore))]
        private static class TileScored
        {
            private static void Postfix(int scoreToShow, TileScript tile)
            {
                if (tile == null) return;
                Say(Strings.BreakdownTile(Round.TileName(tile), scoreToShow));
            }
        }

        [HarmonyPatch(typeof(SpecialTileManager), nameof(SpecialTileManager.PingEmerald))]
        private static class EmeraldHit
        {
            private static void Postfix() => Say(Strings.BreakdownEmerald);
        }

        [HarmonyPatch(typeof(SpecialTileManager), nameof(SpecialTileManager.PlayGoldSubmissionSound))]
        private static class GoldenCounted
        {
            private static void Postfix(int goldMulti)
            {
                _golden = goldMulti;
                Say(Strings.BreakdownGolden(goldMulti));
            }
        }

        [HarmonyPatch(typeof(SpecialTileManager), nameof(SpecialTileManager.PingDot))]
        private static class DotApplied
        {
            private static void Postfix(float __result) => Say(Strings.BreakdownDot(Number(__result)));
        }

        [HarmonyPatch(typeof(ScoreZoneScript), nameof(ScoreZoneScript.FinishGold))]
        private static class GoldenApplied
        {
            private static void Postfix(float combinedScores) => Say(Strings.BreakdownGoldenApplied(_golden, Number(combinedScores)));
        }

        [HarmonyPatch(typeof(ModifierStubScript), nameof(ModifierStubScript.PingModifierStub))]
        private static class ModifierFlashed
        {
            private static void Postfix(ModifierStubScript __instance)
            {
                if (__instance == null || !__instance.hasAPurchasedModifer) return;
                if (_phase == Phase.Accepted)
                {
                    if (!_held.Contains(__instance)) _held.Add(__instance);
                    return;
                }
                _held.Remove(__instance);
                Say(StubText(__instance));
            }
        }

        [HarmonyPatch(typeof(BarScript), nameof(BarScript.ChangeFinalScore))]
        private static class Total
        {
            private static void Postfix(int newNumber)
            {
                var submit = SubmitWord.Instance;
                int letters = submit != null ? Private.Get<int>(submit, "wordScoreForLog") : newNumber;
                int bonus = submit != null ? Private.Get<int>(submit, "bonusScoreForLog") : 0;
                SayHeld();
                Say(Strings.BreakdownTotal(letters, bonus, newNumber));
            }
        }

        [HarmonyPatch(typeof(BarScript), nameof(BarScript.ChangeFinalFinalScore))]
        private static class TotalChanged
        {
            private static void Postfix(float newNumber) => Say(Strings.BreakdownTotalChanged(Number(newNumber)));
        }

        [HarmonyPatch(typeof(LifeBarScript), nameof(LifeBarScript.PopupText))]
        private static class PlaysPopup
        {
            private static void Postfix(string textToShow)
            {
                if (Round.Playing) Say(Strings.BreakdownPlays(textToShow));
            }
        }

        private static string Number(float value) => UnityEngine.Mathf.RoundToInt(value).ToString();
    }
}
