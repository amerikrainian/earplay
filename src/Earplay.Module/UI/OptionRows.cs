using System.Collections.Generic;
using Earplay.Core.Graph;
using Earplay.Core.Strings;
using Earplay.Core.UI;
using Earplay.Module.Interop;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Earplay.Module.UI
{
    /// <summary>
    /// The game's option rows, shared by the title's Options page and the round's Options drawer:
    /// <list type="bullet">
    /// <item>an <c>OptionTutorial</c> button is a toggle whose state is its <c>ticked</c> flag, TRUE
    /// when the option is OFF (its <c>Start</c> crosses the box for it);</item>
    /// <item>a row holding a <c>Slider</c> (volume, 0 to 1) is a slider read as a percentage;</item>
    /// <item>a <c>MoveableButton</c> (Delete Save, Give Up, Quit) is its button, then its Sure? button
    /// while that is slid out (<c>sureShowing</c>);</item>
    /// <item>any other shown button is a plain button.</item>
    /// </list>
    /// </summary>
    internal static class OptionRows
    {
        /// <summary>Add the node(s) for one row; false when the row is nothing we read.</summary>
        public static bool Add(GraphBuilder b, Transform row, string key)
        {
            if (!row.gameObject.activeInHierarchy) return false;
            var mover = row.GetComponent<MoveableButton>();
            if (mover != null)
            {
                AddButton(b, key, Private.Get<Button>(mover, "myButton"));
                if (Private.Get<bool>(mover, "sureShowing"))
                    AddButton(b, key + ":sure", Private.Get<Button>(mover, "sureButton"));
                return true;
            }
            var slider = row.GetComponentInChildren<Slider>(false);
            if (slider != null)
            {
                var label = row.Find("Label") != null ? row.Find("Label").GetComponent<TMP_Text>() : null;
                b.AddItem(ControlId.Structural(key), Slider(slider, () => label != null ? label.text : row.name));
                return true;
            }
            var button = row.GetComponent<Button>();
            if (button == null || !GameNodes.IsShown(button)) return false;
            var aid = row.GetComponent<OptionTutorial>();
            b.AddItem(ControlId.Structural(key), aid != null ? Aid(button, aid) : GameNodes.Button(button));
            return true;
        }

        public static void AddButton(GraphBuilder b, string key, Button button)
        {
            if (!GameNodes.IsShown(button)) return;
            b.AddItem(ControlId.Structural(key), GameNodes.Button(button));
        }

        /// <summary>A volume slider, read as a percentage of its range.</summary>
        public static NodeVtable Slider(Slider slider, System.Func<string> label)
            => GameNodes.Slider(slider, label,
                () => Strings.Percent(Mathf.RoundToInt(Mathf.InverseLerp(slider.minValue, slider.maxValue, slider.value) * 100f)));

        /// <summary>An OptionTutorial button as a toggle.</summary>
        public static NodeVtable Aid(Button button, OptionTutorial aid)
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
    }
}
