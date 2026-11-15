using System.Collections.Generic;
using Earplay.Core;
using Earplay.Core.Graph;
using Earplay.Core.Strings;
using Earplay.Core.UI;
using Earplay.Module.UI;
using TMPro;
using UnityEngine;
using Screen = Earplay.Core.Screens.Screen;

namespace Earplay.Module.Screens.Title
{
    /// <summary>
    /// The Extras page: the Stats panel (Total Words Spelled, Longest Ever Word, Highest-Scoring Word,
    /// Games Won), each row its label and its value as one line, an empty value (nothing spelled yet)
    /// read as blank; the Credits panel line by line. Each panel is a stop named by its own title and
    /// ends with Back (the page's one button, under both). Escape presses Back.
    /// </summary>
    public sealed class ExtrasScreen : Screen
    {
        public override string Key => "title.extras";
        public override string ScreenName => Strings.ScreenExtras;

        public override bool IsActive() => TitlePages.Showing(TitleControl.TitleControlZone.ExtrasScreen);

        private static GameObject Page()
        {
            var m = TitlePages.Manager;
            return m != null ? m.extrasGO : null;
        }

        public override void Build(GraphBuilder b)
        {
            var page = Page();
            if (page == null) return;
            var back = TitlePages.BackButton(page);
            var panels = page.transform.Find("Panels");
            if (panels != null)
            {
                for (int p = 0; p < panels.childCount; p++)
                {
                    var panel = panels.GetChild(p);
                    if (!panel.gameObject.activeInHierarchy) continue;
                    var title = TitlePages.TextAt(panel, "Label Area/Label/Title");
                    b.BeginStop("extras:" + panel.name);
                    b.PushContext(title != null ? title.text : panel.name);
                    var stats = panel.Find("Stat Area");
                    if (stats != null) AddStats(b, stats);
                    else AddLines(b, panel, title);
                    // Back closes each panel, so it is never a stop of its own.
                    TitlePages.AddButton(b, "extras:back:" + panel.name, back);
                    b.PopContext();
                }
            }
        }

        // One line per stat row: "Games Won: 0". The highest-scoring word's row has two values (the
        // word and its score), joined in order.
        private static void AddStats(GraphBuilder b, Transform area)
        {
            for (int i = 0; i < area.childCount; i++)
            {
                var row = area.GetChild(i);
                if (!row.gameObject.activeInHierarchy) continue;
                var label = TitlePages.TextAt(row, "Label");
                var values = row.Find("Area");
                if (label == null || values == null) continue;
                var texts = values.GetComponentsInChildren<TMP_Text>(false);
                b.AddItem(ControlId.Structural("extras:stat:" + row.name), GameNodes.Text(() => Strings.StatLine(label.text, Join(texts))));
            }
        }

        private static string Join(TMP_Text[] texts)
        {
            var parts = new List<string>();
            foreach (var t in texts)
                if (t != null && !string.IsNullOrWhiteSpace(t.text)) parts.Add(t.text.Trim());
            return parts.Count > 0 ? string.Join(" ", parts) : Strings.ValueBlank;
        }

        // A text panel (the credits): every line of its texts but the title, in order.
        private static void AddLines(GraphBuilder b, Transform panel, TMP_Text title)
        {
            int n = 0;
            foreach (var text in panel.GetComponentsInChildren<TMP_Text>(false))
            {
                if (text == title) continue;
                foreach (var line in TitlePages.Lines(text.text))
                {
                    string l = line;
                    b.AddItem(ControlId.Structural("extras:" + panel.name + ":" + n++), GameNodes.Text(() => l));
                }
            }
        }

        public override IEnumerable<ElementAction> GetActions()
        {
            yield return new ElementAction(ActionIds.Back, Strings.Get("bind.ui.back"), _ => TitlePages.Back(Page()));
        }
    }
}
