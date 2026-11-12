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

namespace Earplay.Module.Screens.Title
{
    /// <summary>
    /// The Options page: three panels, each a stop named by its own title, then Back.
    /// <list type="bullet">
    /// <item>Options: Toggle Full Screen (with the window's state after it), the SFX and Music sliders
    /// (0 to 100 percent; Left/Right step a tenth), the resolution dropdown (named by the mod: the game
    /// draws only the current value).</item>
    /// <item>Accessibility: the game's own aids, each a button whose <c>OptionTutorial</c> keeps the
    /// state (its <c>ticked</c> flag is true when the aid is OFF: <c>Start</c> crosses the box for
    /// it), read as toggles; and the font switcher (<c>DyslexicToggle</c>), whose current font is an
    /// icon alone, read by the font's family name off <c>AXManager.fontsToUse</c> (index 0 is the game's
    /// default and reads off); Enter and Right step to the next font, Left to the previous. The three
    /// example tiles above them are a preview and are left out.</item>
    /// <item>More...: Report an Issue (opens the studio's page in the browser) and Delete Save, whose
    /// press slides out a Sure? button (<c>MoveableButton</c>); Sure? is listed only while it is out,
    /// and pressing it wipes every save and reloads the title scene.</item>
    /// </list>
    /// Escape presses Back.
    /// </summary>
    public sealed class OptionsScreen : Screen
    {
        public override string Key => "title.options";
        // No ScreenName: the first panel's own title ("Options") names the page on entry.

        public override bool IsActive() => TitlePages.Showing(TitleControl.TitleControlZone.OptionsScreen);

        private static GameObject Page()
        {
            var m = TitlePages.Manager;
            return m != null ? m.optionsGO : null;
        }

        public override void Build(GraphBuilder b)
        {
            var page = Page();
            if (page == null) return;
            var panels = page.transform.Find("Panels");
            // The panels in reading order: the game's hierarchy lists them right to left.
            AddPanel(b, panels, "Options Panel", "Options", AddOptions);
            AddPanel(b, panels, "AX Panel", "Accessibility", AddAccessibility);
            AddPanel(b, panels, "Other Panel", "Options", AddMore);
            b.BeginStop("options:back");
            TitlePages.AddButton(b, "options:back", TitlePages.BackButton(page));
        }

        private static void AddPanel(GraphBuilder b, Transform panels, string name, string content,
            System.Action<GraphBuilder, Transform> add)
        {
            var panel = panels != null ? panels.Find(name) : null;
            if (panel == null || !panel.gameObject.activeInHierarchy) return;
            var title = TitlePages.TextAt(panel, "Label Area/Label/Title");
            var items = panel.Find(content);
            if (items == null) return;
            b.BeginStop("options:" + name);
            b.PushContext(title != null ? title.text : name);
            add(b, items);
            b.PopContext();
        }

        // ---- Options ----

        private static void AddOptions(GraphBuilder b, Transform items)
        {
            for (int i = 0; i < items.childCount; i++)
            {
                var row = items.GetChild(i);
                if (!row.gameObject.activeInHierarchy) continue;
                string key = "options:" + row.name;
                var slider = row.GetComponentInChildren<Slider>(false);
                var dropdown = row.GetComponent<TMP_Dropdown>();
                var button = row.GetComponent<Button>();
                if (slider != null)
                {
                    var label = TitlePages.TextAt(row, "Label");
                    b.AddItem(ControlId.Structural(key), GameNodes.Slider(slider,
                        () => label != null ? label.text : row.name,
                        () => Strings.Percent(Mathf.RoundToInt(Mathf.InverseLerp(slider.minValue, slider.maxValue, slider.value) * 100f))));
                }
                else if (dropdown != null)
                {
                    b.AddItem(ControlId.Structural(key), GameNodes.Dropdown(dropdown, () => Strings.TitleResolution));
                }
                else if (button != null && button.GetComponent<TitleCustomButton>() == null)
                {
                    var vt = GameNodes.Button(button);
                    // The full-screen switch: the window's state, live (it changes a frame or more later).
                    if (button.onClick.GetPersistentEventCount() > 0 && button.onClick.GetPersistentMethodName(0) == "ToggleFullScreen")
                        vt.Announcements = new List<NodeAnnouncement>(vt.Announcements)
                        {
                            new NodeAnnouncement(() => UnityEngine.Screen.fullScreen ? Strings.StateOn : Strings.StateOff,
                                live: true, kind: AnnouncementKinds.Value),
                        };
                    if (GameNodes.IsShown(button)) b.AddItem(ControlId.Structural(key), vt);
                }
            }
        }

        // ---- Accessibility ----

        private static void AddAccessibility(GraphBuilder b, Transform items)
        {
            for (int i = 0; i < items.childCount; i++)
            {
                var row = items.GetChild(i);
                if (!row.gameObject.activeInHierarchy) continue;
                var aid = row.GetComponent<OptionTutorial>();
                var font = row.GetComponent<DyslexicToggle>();
                var button = row.GetComponent<Button>();
                if (button == null) continue;
                if (aid != null) b.AddItem(ControlId.Structural("options:" + row.name), Aid(button, aid));
                else if (font != null) b.AddItem(ControlId.Structural("options:" + row.name), Font(button, font));
            }
        }

        private static NodeVtable Aid(Button button, OptionTutorial aid)
        {
            System.Func<string> label = () => GameNodes.LabelOf(button);
            System.Func<string> state = () => Private.Get<bool>(aid, "ticked") ? Strings.StateOff : Strings.StateOn;
            return new NodeVtable
            {
                ControlType = ControlTypes.Toggle,
                Announcements = new List<NodeAnnouncement>
                {
                    GameNodes.LabelPart(label),
                    new NodeAnnouncement(state, live: true, kind: AnnouncementKinds.Value),
                    GameNodes.DisabledPart(() => button.interactable),
                },
                SearchText = label,
                OnActivate = () => { if (button.interactable) button.onClick.Invoke(); },
                StateText = state,
            };
        }

        private static NodeVtable Font(Button button, DyslexicToggle font)
        {
            System.Func<string> label = () => GameNodes.LabelOf(button);
            System.Func<string> value = () =>
            {
                int id = Private.Get<int>(font, "currentInt");
                var fonts = AXManager.Instance != null ? AXManager.Instance.fontsToUse : null;
                if (id <= 0 || fonts == null || id >= fonts.Count || fonts[id] == null) return Strings.StateOff;
                return fonts[id].faceInfo.familyName;
            };
            return new NodeVtable
            {
                ControlType = ControlTypes.ComboBox,
                Announcements = new List<NodeAnnouncement>
                {
                    GameNodes.LabelPart(label),
                    new NodeAnnouncement(value, live: true, kind: AnnouncementKinds.Value),
                },
                SearchText = label,
                OnActivate = () => button.onClick.Invoke(),
                OnAdjust = (sign, large) =>
                {
                    if (sign < 0) font.PreviousController();
                    else font.NextFont();
                },
                StateText = value,
            };
        }

        // ---- More... ----

        private static void AddMore(GraphBuilder b, Transform items)
        {
            for (int i = 0; i < items.childCount; i++)
            {
                var row = items.GetChild(i);
                if (!row.gameObject.activeInHierarchy) continue;
                var mover = row.GetComponent<MoveableButton>();
                if (mover != null)
                {
                    TitlePages.AddButton(b, "options:delete", Private.Get<Button>(mover, "myButton"));
                    if (Private.Get<bool>(mover, "sureShowing"))
                        TitlePages.AddButton(b, "options:sure", Private.Get<Button>(mover, "sureButton"));
                    continue;
                }
                TitlePages.AddButton(b, "options:" + row.name, row.GetComponent<Button>());
            }
        }

        public override IEnumerable<ElementAction> GetActions()
        {
            yield return new ElementAction(ActionIds.Back, Strings.Get("bind.ui.back"), _ => TitlePages.Back(Page()));
        }
    }
}
