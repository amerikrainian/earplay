using System.Collections.Generic;
using Earplay.Core;
using Earplay.Core.Graph;
using Earplay.Core.Strings;
using Earplay.Core.UI;
using Earplay.Module.Interop;
using Earplay.Module.UI;
using UnityEngine;
using Screen = Earplay.Core.Screens.Screen;

namespace Earplay.Module.Screens.Game
{
    /// <summary>
    /// The round's Options drawer (Escape on the round: <c>TopDrawerManager.ToggleOptionsDrawer</c>,
    /// the game's own Escape): Give Up and Save and Quit (each a first press that slides out Sure?, a
    /// second press on Sure? that does it), Report an Issue, Toggle Display Scale, the full-screen
    /// switch, Controller Icons, Music and SFX, Show Tooltips, the shuffle default, in the drawer's
    /// order. Active while the drawer is out; a modal (the keyboard is the mod's again). Escape closes
    /// it with the same toggle.
    /// </summary>
    public sealed class OptionsDrawerScreen : Screen
    {
        public override string Key => "game.options";
        public override int Layer => 20;
        public override bool Exclusive => true;

        private static DrawerScript Drawer()
        {
            var top = TopDrawerManager.Instance;
            return top != null ? Private.Get<DrawerScript>(top, "optionsDrawer") : null;
        }

        public override bool IsActive()
        {
            var drawer = Drawer();
            return drawer != null && Private.Get<bool>(drawer, "drawerIsOut");
        }

        public override void Build(GraphBuilder b)
        {
            var drawer = Drawer();
            var panel = drawer != null ? drawer.transform.Find("Drawer/Panel") : null;
            if (panel == null) return;
            b.PushContext(Strings.TitleOptions);
            for (int i = 0; i < panel.childCount; i++)
                OptionRows.Add(b, panel.GetChild(i), "options:" + panel.GetChild(i).name);
            b.PopContext();
        }

        public override IEnumerable<ElementAction> GetActions()
        {
            yield return new ElementAction(ActionIds.Back, Strings.Get("bind.ui.back"), _ =>
            {
                if (TopDrawerManager.Instance != null) TopDrawerManager.Instance.ToggleOptionsDrawer();
            });
        }
    }
}
