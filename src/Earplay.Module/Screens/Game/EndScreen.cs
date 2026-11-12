using System.Collections.Generic;
using Earplay.Core.Graph;
using Earplay.Core.Strings;
using Earplay.Core.UI;
using Earplay.Module.Interop;
using Earplay.Module.Round;
using Earplay.Module.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Screen = Earplay.Core.Screens.Screen;

namespace Earplay.Module.Screens.Game
{
    /// <summary>
    /// The end of a game (<c>GameOverScreen</c>, held by <c>LogicManagerScript.gOScreen</c>; the control
    /// zone is EndScreen): its title ("You Win!", "You Lose...", "Not Enough Tiles!") names the screen,
    /// then every shown text of the panel in order (the stats: "Words Spelled: 39", "Perks Purchased:
    /// 9", "Best Word: PADDING - 97", the reached round or final score, a new best), then its buttons
    /// through the shared option rows (Endless Mode or Play Again; Quit to Menu, whose press slides out
    /// Sure?). One stop. Escape does nothing: the game asks for a choice.
    /// </summary>
    public sealed class EndScreen : Screen
    {
        public override string Key => "game.end";

        private static GameOverScreen End()
            => LogicManagerScript.Instance != null ? Private.Get<GameOverScreen>(LogicManagerScript.Instance, "gOScreen") : null;

        private static GameObject Panel()
        {
            var end = End();
            var panel = end != null ? Private.Get<GameObject>(end, "gameOverScreen") : null;
            return panel != null && panel.activeInHierarchy ? panel : null;
        }

        public override bool IsActive()
            => Panel() != null && GamepadControl.Instance != null
               && GamepadControl.Instance.currentControlZone == GamepadControl.ControlZone.EndScreen;

        public override void Build(GraphBuilder b)
        {
            var end = End();
            var panel = Panel();
            if (end == null || panel == null) return;
            var title = Private.Get<TMP_Text>(end, "topText");
            b.PushContext(title != null && !string.IsNullOrWhiteSpace(title.text) ? GameNodesText.Lines(title.text)[0] : null, null, positions: false);

            int n = 0;
            foreach (var text in panel.GetComponentsInChildren<TMP_Text>(false))
            {
                if (text == title || text.GetComponentInParent<Button>() != null) continue;
                foreach (var line in GameNodesText.Lines(text.text))
                {
                    string l = line;
                    b.AddItem(ControlId.Structural("end:text" + n++), GameNodes.Text(() => l));
                }
            }

            var buttons = panel.transform.Find("Panel/Buttons");
            if (buttons != null)
                for (int i = 0; i < buttons.childCount; i++)
                    OptionRows.Add(b, buttons.GetChild(i), "end:" + buttons.GetChild(i).name);
            b.PopContext();
        }
    }
}
