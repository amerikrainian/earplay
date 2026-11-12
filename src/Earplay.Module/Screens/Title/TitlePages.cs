using System.Collections.Generic;
using Earplay.Core.Graph;
using Earplay.Module.Interop;
using Earplay.Module.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Earplay.Module.Screens.Title
{
    /// <summary>
    /// The title scene's pages. The scene holds five of them side by side under one canvas (the logo
    /// page with the main menu, the consent page, the level select, options, extras), all active at
    /// once: <c>TitleScreenManager.OpenPage</c> cross-fades their CanvasGroups and, first thing, sets
    /// the scene's <c>TitleControl.currentControlZone</c> to the page coming in. That zone is the one
    /// fact of which page is the player's, so each page screen is active exactly while it names its
    /// page, from the press that opened it (the fade in is a third of a second; the old page's buttons
    /// stop taking clicks only at its end, which no keyboard press can reach first).
    /// </summary>
    internal static class TitlePages
    {
        /// <summary>The title scene's manager, or null outside the title scene (Unity's null: a
        /// destroyed manager from the last visit compares equal to null).</summary>
        public static TitleScreenManager Manager
        {
            get
            {
                var m = TitleScreenManager.Instance;
                return m != null ? m : null;
            }
        }

        /// <summary>Whether the title scene is up with <paramref name="zone"/> as its page.</summary>
        public static bool Showing(TitleControl.TitleControlZone zone)
        {
            var m = Manager;
            if (m == null) return false;
            var control = Private.Get<TitleControl>(m, "controlScript");
            return control != null && control.currentControlZone == zone;
        }

        /// <summary>The page's Back button (every page but the logo page has one, named so, a direct
        /// child of the page), which the game's own Escape presses through <c>ReturnToHome</c>.</summary>
        public static Button BackButton(GameObject page)
        {
            var t = page != null ? page.transform.Find("Back Button") : null;
            return t != null ? t.GetComponent<Button>() : null;
        }

        /// <summary>Press a page's Back button, as the game's Escape does.</summary>
        public static void Back(GameObject page)
        {
            var back = BackButton(page);
            if (GameNodes.IsShown(back) && back.interactable) back.onClick.Invoke();
        }

        /// <summary>A Button node for <paramref name="button"/> when it is shown.</summary>
        public static void AddButton(GraphBuilder b, string key, Button button)
        {
            if (!GameNodes.IsShown(button)) return;
            b.AddItem(ControlId.Structural(key), GameNodes.Button(button));
        }

        /// <summary>The text of a TMP child by path, or null.</summary>
        public static TMP_Text TextAt(Transform root, string path)
        {
            var t = root != null ? root.Find(path) : null;
            return t != null ? t.GetComponent<TMP_Text>() : null;
        }

        /// <summary>The non-blank lines of a text as the game draws it, each a separate line.</summary>
        public static List<string> Lines(string text)
        {
            var lines = new List<string>();
            if (string.IsNullOrEmpty(text)) return lines;
            foreach (var line in text.Split('\n'))
            {
                string clean = Earplay.Contracts.TextFilter.Clean(line);
                if (clean.Length > 0) lines.Add(clean);
            }
            return lines;
        }
    }
}
