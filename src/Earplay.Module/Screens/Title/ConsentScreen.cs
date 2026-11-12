using Earplay.Core.Graph;
using Earplay.Core.Strings;
using Earplay.Module.UI;
using TMPro;
using UnityEngine.UI;
using Screen = Earplay.Core.Screens.Screen;

namespace Earplay.Module.Screens.Title
{
    /// <summary>
    /// The analytics consent page ("Hey there! Word Play collects anonymised data..."), with its No
    /// Thanks and Okay! buttons. The game opens it through <c>OpenConsentScreen</c> before the level
    /// select while the player has never answered (no <c>consented</c> pref); this build's Play goes
    /// straight to the level select, so the page may never show, but it is read if it does: its message
    /// line by line, then the buttons. Never pressed for the player; Escape does nothing, as in the game.
    /// </summary>
    public sealed class ConsentScreen : Screen
    {
        public override string Key => "title.consent";

        public override bool IsActive() => TitlePages.Showing(TitleControl.TitleControlZone.ConsentScreen);

        public override void Build(GraphBuilder b)
        {
            var m = TitlePages.Manager;
            var page = m != null ? m.consentGO : null;
            if (page == null) return;
            b.PushContext(Strings.ScreenConsent);
            int n = 0;
            foreach (var text in page.GetComponentsInChildren<TMP_Text>(false))
            {
                if (text.GetComponentInParent<Button>() != null) continue; // a caption reads with its button
                foreach (var line in TitlePages.Lines(text.text))
                {
                    string l = line;
                    b.AddItem(ControlId.Structural("consent:text" + n++), GameNodes.Text(() => l));
                }
            }
            foreach (var button in page.GetComponentsInChildren<Button>(false))
                TitlePages.AddButton(b, "consent:" + button.gameObject.name, button);
            b.PopContext();
        }
    }
}
