using System;
using System.IO;
using System.Text;
using Earplay.Contracts;
using Earplay.Core;
using Earplay.Core.Graph;
using Earplay.Core.Input;
using Earplay.Core.Screens;
using Earplay.Core.Strings;
using Earplay.Core.UI;
using Earplay.Module.Input;
using Earplay.Module.Screens;
using Earplay.Module.UI;
using HarmonyLib;
using UnityEngine;

namespace Earplay.Module
{
    /// <summary>
    /// The reloadable feature module: wires the Core seams to the host, registers the mod's input
    /// actions and screens, owns focus mode, and drives the per-frame loop. Everything here is torn down
    /// in <see cref="Dispose"/> so a fresh generation can hot-swap in without a restart. Core statics are
    /// per-generation (each generation loads its own renamed copy of Core), so they need no manual reset.
    /// </summary>
    public sealed class ModuleMain : IModModule, IDevDriver
    {
        private IModHost _host;

        /// <summary>The host of the live generation (settings, logging), for screens that need it.</summary>
        public static IModHost Host { get; private set; }
        private Harmony _harmony;
        // The launch update check: asked once the module is up, its line spoken from Tick when the
        // request lands with a release newer than the running build.
        private readonly UpdateChecker _updateCheck = new UpdateChecker();
        private bool _updateAnnounced;

        public void Load(IModHost host)
        {
            _host = host;
            Host = host;

            // Core seams: logging, speech, engine inputs, focus ownership, announcement wording.
            CoreLog.Info = host.LogInfo;
            CoreLog.Warning = host.LogWarning;
            CoreLog.Error = host.LogError;
            Speech.Speak = (text, interrupt) => host.Speech.Speak(text, interrupt);
            NavInput.Current = new UnityNavInput();
            Navigation.FocusActive = () => FocusMode.Active;
            InputManager.FocusActive = () => FocusMode.Active;
            InputManager.TextFieldFocused = () => TextEdit.OwnsKeyboard;
            GraphAnnouncer.PositionText = (i, n) => host.Settings.SpeakPositions ? Strings.Position(i, n) : null;
            GraphAnnouncer.ExpandedStateText = Strings.ExpandedState;
            InputBinding.RegisterType("keyboard", KeyboardBinding.Deserialize);
            // Word Play is English only: the mod's one override file (lang/strings.txt, the template is
            // lang/en.txt) is read at every load, so a reload picks up an edit.
            LoadStrings(host);

            // A per-load UNIQUE id so a reload's Dispose unpatches exactly this load's patches: the host
            // loads the new module (which patches) before disposing the old one, and UnpatchSelf removes
            // by owner id, so a fixed id would let the old teardown strip the fresh load's patches.
            _harmony = new Harmony("com.amerikranian.earplay.module." + Guid.NewGuid().ToString("N"));

            RegisterInput();
            RegisterScreens();
            // The game hooks. Patched by this load's Harmony id, unpatched in Dispose.
            try { _harmony.PatchAll(typeof(ModuleMain).Assembly); }
            catch (Exception e) { host.LogError("Harmony patching failed: " + e); }
            FocusMode.Set(host.Settings.FocusModeOnLaunch);
            host.LogInfo("Module loaded: " + ScreenManager.Registered.Count + " screens, "
                + InputManager.Actions.Count + " input actions, focus " + (FocusMode.Active ? "on" : "off"));
            _updateCheck.Start(host.ModVersion);
            AnnounceLaunch();
        }

        private static void LoadStrings(IModHost host)
        {
            string path = Path.Combine(Path.Combine(host.PluginDir, "lang"), "strings.txt");
            if (!File.Exists(path)) { Strings.LoadTranslation(null); return; }
            try
            {
                var report = Strings.LoadTranslation(File.ReadAllLines(path));
                host.LogInfo("Strings: " + path + ": " + report);
            }
            catch (Exception e)
            {
                host.LogWarning("Strings: could not read " + path + ": " + e.Message);
                Strings.LoadTranslation(null);
            }
        }

        // "Earplay 0.0.1 loaded", once per launch: the host leaves the line to the module, which has
        // the strings. The host remembers it was said, so a reload stays quiet.
        private void AnnounceLaunch()
        {
            if (_host.LaunchAnnounced) return;
            _host.LaunchAnnounced = true;
            Speech.Say(Strings.ModLoaded(_host.ModVersion));
        }

        private static void RegisterInput()
        {
            // UI navigation keys: live only while a screen declares the UI category and focus mode is
            // on, routed into the navigator by InputManager. Directions and Tab auto-repeat while held.
            InputManager.Register(UiActions.Up, "Navigate up", InputCategory.UI).AddBinding(new KeyboardBinding(KeyCode.UpArrow)).Repeating();
            InputManager.Register(UiActions.Down, "Navigate down", InputCategory.UI).AddBinding(new KeyboardBinding(KeyCode.DownArrow)).Repeating();
            InputManager.Register(UiActions.Left, "Navigate left", InputCategory.UI).AddBinding(new KeyboardBinding(KeyCode.LeftArrow)).Repeating();
            InputManager.Register(UiActions.Right, "Navigate right", InputCategory.UI).AddBinding(new KeyboardBinding(KeyCode.RightArrow)).Repeating();
            InputManager.Register(UiActions.Next, "Next control group", InputCategory.UI).AddBinding(new KeyboardBinding(KeyCode.Tab)).Repeating();
            InputManager.Register(UiActions.Prev, "Previous control group", InputCategory.UI).AddBinding(new KeyboardBinding(KeyCode.Tab, shift: true)).Repeating();
            InputManager.Register(UiActions.Activate, "Activate", InputCategory.UI)
                .AddBinding(new KeyboardBinding(KeyCode.Return)).AddBinding(new KeyboardBinding(KeyCode.KeypadEnter));
            InputManager.Register(UiActions.Secondary, "Secondary action", InputCategory.UI).AddBinding(new KeyboardBinding(KeyCode.Backspace));
            InputManager.Register(UiActions.Back, "Back", InputCategory.UI).AddBinding(new KeyboardBinding(KeyCode.Escape));
            InputManager.Register(UiActions.Home, "Jump to first", InputCategory.UI).AddBinding(new KeyboardBinding(KeyCode.Home));
            InputManager.Register(UiActions.End, "Jump to last", InputCategory.UI).AddBinding(new KeyboardBinding(KeyCode.End));
            InputManager.Register(UiActions.RegionPrev, "Previous section", InputCategory.UI).AddBinding(new KeyboardBinding(KeyCode.UpArrow, alt: true));
            InputManager.Register(UiActions.RegionNext, "Next section", InputCategory.UI).AddBinding(new KeyboardBinding(KeyCode.DownArrow, alt: true));
            // Space: pick up the focused tile, then drop it on another (the game's drag, by keyboard).
            InputManager.Register(UiActions.Drag, "Pick up or drop a tile", InputCategory.UI).AddBinding(new KeyboardBinding(KeyCode.Space));
            // Delete: the focused thing's removal (a word slot's tile back to the board).
            InputManager.Register(UiActions.Delete, "Return the tile to the board", InputCategory.UI).AddBinding(new KeyboardBinding(KeyCode.Delete));
            // The round's word keys, answered by the round screen alone (no handler here).
            InputManager.Register(Round.RoundScreen.ClearAction, "Clear the word", InputCategory.UI).AddBinding(new KeyboardBinding(KeyCode.Backspace, shift: true));
            InputManager.Register(Round.RoundScreen.CloseGapsAction, "Close gaps", InputCategory.UI).AddBinding(new KeyboardBinding(KeyCode.Backspace, ctrl: true));
            // Alt+W: the word strip's first slot, from anywhere on the board.
            InputManager.Register(Round.RoundScreen.WordAction, "Jump to the word", InputCategory.UI).AddBinding(new KeyboardBinding(KeyCode.W, alt: true));
            // Shift+Enter: submit the word from anywhere on the round.
            InputManager.Register(Round.RoundScreen.SubmitAction, "Submit the word", InputCategory.UI)
                .AddBinding(new KeyboardBinding(KeyCode.Return, shift: true)).AddBinding(new KeyboardBinding(KeyCode.KeypadEnter, shift: true));
            // The game's tool keys, ours on the round so the key help lists and runs them.
            InputManager.Register(Round.RoundScreen.ShuffleAction, "Shuffle", InputCategory.UI)
                .AddBinding(new KeyboardBinding(KeyCode.Alpha2)).AddBinding(new KeyboardBinding(KeyCode.Keypad2));
            InputManager.Register(Round.RoundScreen.ShuffleOtherAction, "Sort", InputCategory.UI)
                .AddBinding(new KeyboardBinding(KeyCode.Alpha2, shift: true)).AddBinding(new KeyboardBinding(KeyCode.Keypad2, shift: true));
            InputManager.Register(Round.RoundScreen.RefreshAction, "Refresh, held", InputCategory.UI)
                .AddBinding(new KeyboardBinding(KeyCode.Alpha3)).AddBinding(new KeyboardBinding(KeyCode.Keypad3));
            InputManager.Register(Round.RoundScreen.WildcardAction, "Wildcard tile", InputCategory.UI)
                .AddBinding(new KeyboardBinding(KeyCode.Alpha8)).AddBinding(new KeyboardBinding(KeyCode.Keypad8));
            InputManager.Register(Round.RoundScreen.PlusAction, "Plus tile", InputCategory.UI)
                .AddBinding(new KeyboardBinding(KeyCode.Equals)).AddBinding(new KeyboardBinding(KeyCode.KeypadPlus));
            // Buffer review, the Harkest Dungeon keys: Ctrl+Left/Right switch buffers, Ctrl+Up/Down step lines.
            Buffers.Init();
            InputManager.Register("buffer.next", "Next buffer", InputCategory.UI, Buffers.Controls.NextBuffer).AddBinding(new KeyboardBinding(KeyCode.RightArrow, ctrl: true));
            InputManager.Register("buffer.prev", "Previous buffer", InputCategory.UI, Buffers.Controls.PreviousBuffer).AddBinding(new KeyboardBinding(KeyCode.LeftArrow, ctrl: true));
            InputManager.Register("buffer.line.next", "Next buffer line", InputCategory.UI, Buffers.Controls.NextLine).AddBinding(new KeyboardBinding(KeyCode.UpArrow, ctrl: true)).Repeating();
            InputManager.Register("buffer.line.prev", "Previous buffer line", InputCategory.UI, Buffers.Controls.PreviousLine).AddBinding(new KeyboardBinding(KeyCode.DownArrow, ctrl: true)).Repeating();
            // Global: always live, so the player can hand the keyboard back to the game and reclaim it.
            InputManager.Register("mod.focus", "Toggle navigation", InputCategory.Global, ToggleFocus)
                .AddBinding(new KeyboardBinding(KeyCode.A, ctrl: true, shift: true));
            InputManager.Register("mod.menu", "Mod menu", InputCategory.Global, ModMenuScreen.Toggle)
                .AddBinding(new KeyboardBinding(KeyCode.M, ctrl: true, shift: true));
            // The keys that do something here, each runnable from the list.
            InputManager.Register(HelpScreen.ActionKey, "Keys here", InputCategory.Global, HelpScreen.Toggle)
                .AddBinding(new KeyboardBinding(KeyCode.F1));
        }

        private static void ToggleFocus()
        {
            FocusMode.Toggle();
            Speech.Say(FocusMode.Active ? Strings.FocusOn : Strings.FocusOff, interrupt: true);
            if (FocusMode.Active) Navigation.AnnounceCurrent();
        }

        private static void RegisterScreens()
        {
            // The title scene's pages, one screen each, active while the scene's control zone names it.
            ScreenManager.Register(new Screens.Title.MainMenuScreen());
            ScreenManager.Register(new Screens.Title.LevelSelectScreen());
            ScreenManager.Register(new Screens.Title.OptionsScreen());
            ScreenManager.Register(new Screens.Title.ExtrasScreen());
            ScreenManager.Register(new Screens.Title.ConsentScreen());
            // The round, and its modal panels over it.
            ScreenManager.Register(new Round.RoundScreen());
            ScreenManager.Register(new Round.BagScreen());
            ScreenManager.Register(new Screens.Game.ShopScreen());
            ScreenManager.Register(new Round.PeekScreen());
            ScreenManager.Register(new Screens.Game.OptionsDrawerScreen());
            ScreenManager.Register(new Screens.Game.TutorialScreen());
            ScreenManager.Register(new ModMenuScreen());
            ScreenManager.Register(new HelpScreen());
        }

        public void Tick()
        {
            if (!_updateAnnounced && _updateCheck.NewerVersion != null)
            {
                _updateAnnounced = true;
                Speech.Say(Strings.UpdateAvailable(_updateCheck.NewerVersion));
            }
            FocusMode.Tick();
            Safe(TextEdit.Tick, "text edit");
            InputManager.Tick();
            ScreenManager.Tick();
            if (!TextEdit.OwnsKeyboard) Navigation.TickTypeahead();
            Safe(Round.RoundEvents.Tick, "round events");
            Safe(Later.Tick, "later");
            Safe(Buffers.Tick, "buffers");
        }

        // A reader that throws must not take the whole tick (and every other reader) down with it.
        private void Safe(Action tick, string what)
        {
            try { tick(); }
            catch (Exception e) { _host?.LogWarning("[" + what + "] " + e); }
        }

        /// <summary>Undo every persistent game-side effect Load created. Runs on reload (after the new
        /// generation is live) and on shutdown.</summary>
        public void Dispose()
        {
            // On a reload the successor already owns the keyboard and the EventSystem: drop our hooks
            // only. On a shutdown, give everything back to the game.
            bool restore = _host == null || !_host.SuccessorLoaded;
            try { TextEdit.Shutdown(); } catch (Exception e) { _host?.LogError("[dispose] text edit: " + e); }
            try { FocusMode.Shutdown(restore); } catch (Exception e) { _host?.LogError("[dispose] focus: " + e); }
            try { ScreenManager.Shutdown(); } catch (Exception e) { _host?.LogError("[dispose] screens: " + e); }
            try { InputManager.Clear(); } catch (Exception e) { _host?.LogError("[dispose] input: " + e); }
            try { _harmony?.UnpatchSelf(); } catch (Exception e) { _host?.LogError("[dispose] harmony: " + e); }
            _harmony = null;
            _host = null;
            if (ReferenceEquals(Host, this)) Host = null;
        }

        // ---- IDevDriver: the dev server drives and inspects our navigation through these ----

        public string DispatchAction(string actionKey)
        {
            // Not keys: the dev driver's own verbs (the coverage report, typing).
            if (actionKey == "dev.audit") return Dev.ScreenAudit.Run();
            if (actionKey != null && actionKey.StartsWith("dev.type:"))
            {
                Input.UnityNavInput.InjectTyped(actionKey.Substring("dev.type:".Length));
                return "typing next frame";
            }
            if (string.IsNullOrEmpty(actionKey) || InputManager.Find(actionKey) == null) return null;
            InputManager.Dispatch(actionKey);
            var nav = Navigation.Active as GraphNavigator;
            var node = nav?.FocusedNode;
            return "fired " + actionKey + (node != null ? " -> " + GraphAnnouncer.ComposeFull(node) : "");
        }

        public string DescribeNav()
        {
            var sb = new StringBuilder();
            sb.Append("focus mode: ").Append(FocusMode.Active ? "on" : "off").Append('\n');
            sb.Append("stack: ");
            foreach (var s in ScreenManager.Stack) sb.Append(s.Key).Append(' ');
            sb.Append('\n');
            var cur = ScreenManager.Current;
            sb.Append("screen: ").Append(cur != null ? cur.Key + " (" + (cur.ScreenName ?? "") + ")" : "(none)").Append('\n');
            var nav = Navigation.Active as GraphNavigator;
            var node = nav?.FocusedNode;
            if (node == null)
            {
                sb.Append("focus: (none)\n");
                return sb.ToString();
            }
            sb.Append("focus: ").Append(node.Id).Append('\n');
            sb.Append("readout: ").Append(GraphAnnouncer.ComposeFull(node)).Append('\n');
            var render = nav.CurrentRender;
            if (render != null)
            {
                sb.Append("nodes (").Append(render.Order.Count).Append("):\n");
                foreach (var n in render.Order)
                    sb.Append(ReferenceEquals(n, node) ? "  > " : "    ").Append(n.Id.StructuralKey)
                      .Append(": ").Append(GraphAnnouncer.LeafText(n)).Append('\n');
            }
            return sb.ToString();
        }

        public string ListActions()
        {
            var sb = new StringBuilder();
            foreach (var a in InputManager.Actions)
                sb.Append(a.Key).Append(" [").Append(a.Category).Append("] ").Append(a.BindingsDisplay).Append('\n');
            return sb.ToString();
        }
    }
}
