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
using Navigation = Earplay.Core.UI.Navigation;
using Screen = Earplay.Core.Screens.Screen;

namespace Earplay.Module.Screens.Game
{
    /// <summary>
    /// The round's tutorial panel (<c>TutorialManager.tutorialGO</c>, "Tutorial Screen"): the "How to
    /// Play" card at the first round, and the same panel again for every later lesson the game
    /// attaches (<c>AttachTutorial</c>: the shop, modifiers, special tiles). Its title is the
    /// <c>labelTA</c> text; its body is up to three parts (<c>tutorialParts</c>), each a picture tile
    /// beside a line of text (<c>textAreas</c>), a part with no text switched off. The picture tiles
    /// illustrate the line ("4" beside "Words must have at least 4 tiles") and are left out. Then the
    /// panel's buttons ("Got it!"), whose press runs <c>ShutTutorialScreen</c>. A modal: exclusive,
    /// title as the dialog context, the lines read on entry before the landing on the button. Escape
    /// presses the panel's only way out, as the game offers no other.
    /// </summary>
    public sealed class TutorialScreen : Screen
    {
        public override string Key => "game.tutorial";
        public override int Layer => 30;
        public override bool Exclusive => true;

        private static GameObject Panel()
        {
            var t = TutorialManager.Instance;
            if (t == null) return null;
            var go = Private.Get<GameObject>(t, "tutorialGO");
            return go != null && go.activeInHierarchy ? go : null;
        }

        public override bool IsActive() => Panel() != null;

        public override void Build(GraphBuilder b)
        {
            var t = TutorialManager.Instance;
            var panel = Panel();
            if (t == null || panel == null) return;
            var label = Private.Get<Component>(t, "labelTA");
            var title = label != null ? label.GetComponent<TMP_Text>() : null;
            var parts = Private.Get<List<GameObject>>(t, "tutorialParts");
            var texts = Private.Get<List<TMP_Text>>(t, "textAreas");

            b.PushContext(title != null ? title.text : null, Strings.RoleDialog, positions: false);
            for (int i = 0; parts != null && texts != null && i < parts.Count && i < texts.Count; i++)
            {
                if (parts[i] == null || !parts[i].activeInHierarchy || texts[i] == null) continue;
                var text = texts[i];
                if (string.IsNullOrWhiteSpace(text.text)) continue;
                b.AddItem(ControlId.Structural("tutorial:text" + i), GameNodes.Text(() => text.text));
            }
            foreach (var button in Buttons(panel))
                b.AddItem(ControlId.Structural("tutorial:" + button.gameObject.name), GameNodes.Button(button));
            b.PopContext();
        }

        // The panel's captioned buttons, in order.
        private static List<Button> Buttons(GameObject panel)
        {
            var list = new List<Button>();
            foreach (var button in panel.GetComponentsInChildren<Button>(false))
                if (GameNodes.IsShown(button) && button.GetComponentInChildren<TMP_Text>(true) != null)
                    list.Add(button);
            return list;
        }

        public override IEnumerable<ElementAction> GetActions()
        {
            yield return new ElementAction(ActionIds.Back, Strings.Get("bind.ui.back"), _ =>
            {
                var panel = Panel();
                if (panel == null) return;
                var buttons = Buttons(panel);
                if (buttons.Count == 1 && buttons[0].interactable) buttons[0].onClick.Invoke();
                else Navigation.AnnounceCurrent();
            });
        }
    }
}
