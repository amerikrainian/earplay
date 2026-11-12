using System.Collections.Generic;
using Earplay.Core.Graph;
using Earplay.Core.Strings;
using Earplay.Core.UI;
using Earplay.Module.Interop;
using Earplay.Module.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Screen = Earplay.Core.Screens.Screen;

namespace Earplay.Module.Screens.Title
{
    /// <summary>
    /// The main menu: the title scene's logo page. Two stops. The menu: the page's buttons column
    /// (Play, Resume Run, Extras, Quit, in the game's order) with the resume bubble's "Classic,
    /// Round 3" read after Resume Run, then the Daily Challenge tile while the game offers it (it shows
    /// only once Easy Mode is beaten and a challenge exists), then the corner's two icon buttons,
    /// Options and Full screen, which carry no caption and so are named by the mod. The info stop: the
    /// tip (Left/Right step through the tips as the panel's arrow buttons do) and the version line.
    /// The logo's letter tiles are decoration and are left out. Declared fresh from the live page every
    /// render; Escape has no meaning here, as in the game.
    /// </summary>
    public sealed class MainMenuScreen : Screen
    {
        public override string Key => "title.main";
        // No ScreenName: the menu context announces "Main menu" on entry.

        public override bool IsActive() => TitlePages.Showing(TitleControl.TitleControlZone.TitleScreen);

        public override void Build(GraphBuilder b)
        {
            var m = TitlePages.Manager;
            var page = m != null ? m.logoGO : null;
            if (page == null) return;

            b.BeginStop("menu");
            b.PushContext(Strings.ScreenMainMenu);
            var column = page.transform.Find("Buttons");
            if (column != null)
            {
                for (int i = 0; i < column.childCount; i++)
                {
                    var button = column.GetChild(i).GetComponent<Button>();
                    if (!GameNodes.IsShown(button)) continue;
                    var vt = GameNodes.Button(button);
                    if (button == m.resumeButton) vt = WithResumeBubble(vt, m);
                    b.AddItem(ControlId.Structural("title:" + button.gameObject.name), vt);
                }
            }
            AddDailyChallenge(b, page);
            AddIconButton(b, page, "Options/Options/Options Button", "title:options", () => Strings.TitleOptions, null);
            AddIconButton(b, page, "Options/Full Screen/Full Screen Toggle", "title:fullscreen", () => Strings.TitleFullScreen,
                () => UnityEngine.Screen.fullScreen ? Strings.StateOn : Strings.StateOff, ControlTypes.Toggle);
            b.PopContext();

            b.BeginStop("info");
            AddTip(b, page);
            var version = TitlePages.TextAt(page.transform, "Text/Version Number");
            if (version != null && version.gameObject.activeInHierarchy)
                b.AddItem(ControlId.Structural("title:version"), GameNodes.Text(() => version.text));
        }

        // Resume Run is greyed out without a saved run; with one, the bubble beside it names the mode
        // and the round ("Classic, Round 3"), read as the button's description.
        private static NodeVtable WithResumeBubble(NodeVtable vt, TitleScreenManager m)
        {
            var bubble = Private.Get<GameObject>(m, "resumeBubble");
            var text = Private.Get<TMP_Text>(m, "resumeText");
            var parts = new List<NodeAnnouncement>(vt.Announcements)
            {
                GameNodes.TooltipPart(() => bubble != null && bubble.activeInHierarchy && text != null ? text.text : null),
            };
            vt.Announcements = parts;
            return vt;
        }

        // The Daily Challenge: a calendar tile ("NOV", "12") under a "DAILY CHALLENGE" label, with the
        // day's state on a tag above it ("Not Played", "Resume", "4 Words", "Game Over!"). The game
        // hides it by fading its CanvasGroup to nothing and making it non-interactable.
        private static void AddDailyChallenge(GraphBuilder b, GameObject page)
        {
            var script = page.GetComponentInChildren<DailyChallengeScript>(false);
            if (script == null) return;
            var group = Private.Get<CanvasGroup>(script, "DCcanvasGroup");
            var button = Private.Get<Button>(script, "dailyChallengeButton");
            if (group == null || !group.interactable || group.alpha <= 0f || !GameNodes.IsShown(button)) return;
            var root = script.transform;
            var label = TitlePages.TextAt(root, "Label - DC/Label");
            var state = TitlePages.TextAt(root, "Outline/Info Label/Label");
            var month = Private.Get<TMP_Text>(script, "monthPart");
            var day = Private.Get<TMP_Text>(script, "dayPart");
            var vt = GameNodes.Button(button, () => label != null ? label.text : button.gameObject.name);
            vt.Announcements = new List<NodeAnnouncement>(vt.Announcements)
            {
                new NodeAnnouncement(() => month != null && day != null ? month.text + " " + day.text : null, kind: AnnouncementKinds.Value),
                GameNodes.TooltipPart(() => state != null ? state.text : null),
            };
            b.AddItem(ControlId.Structural("title:daily"), vt);
        }

        // A caption-less icon button, named by the mod; a state, when given, is read live after the
        // name (the full-screen switch takes effect a frame or more after the press).
        private static void AddIconButton(GraphBuilder b, GameObject page, string path, string key,
            System.Func<string> label, System.Func<string> state, ControlType type = null)
        {
            var t = page.transform.Find(path);
            var button = t != null ? t.GetComponent<Button>() : null;
            if (!GameNodes.IsShown(button)) return;
            var vt = GameNodes.Button(button, label, type);
            if (state != null)
                vt.Announcements = new List<NodeAnnouncement>(vt.Announcements)
                {
                    new NodeAnnouncement(state, live: true, kind: AnnouncementKinds.Value),
                };
            b.AddItem(ControlId.Structural(key), vt);
        }

        // The tip panel: one tip at a time, cycled by two arrow buttons the game wires to
        // TipSystem.PreviousTip / NextTip. Left/Right press them, and the new tip is the feedback.
        private static void AddTip(GraphBuilder b, GameObject page)
        {
            var tips = page.GetComponentInChildren<TipSystem>(false);
            var area = tips != null ? Private.Get<TMP_Text>(tips, "tipArea") : null;
            if (area == null || !area.gameObject.activeInHierarchy) return;
            System.Func<string> text = () => area.text;
            b.AddItem(ControlId.Structural("title:tip"), new NodeVtable
            {
                ControlType = ControlTypes.Text,
                Announcements = new[] { GameNodes.LabelPart(text) },
                SearchText = text,
                OnAdjust = (sign, large) =>
                {
                    if (sign < 0) tips.PreviousTip();
                    else tips.NextTip();
                },
                StateText = text,
            });
        }
    }
}
