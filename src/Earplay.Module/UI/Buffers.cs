using Earplay.Core;
using Earplay.Core.Buffers;
using Buffer = Earplay.Core.Buffers.Buffer;
using Earplay.Core.Graph;
using Earplay.Core.Strings;
using Earplay.Core.UI;

namespace Earplay.Module.UI
{
    /// <summary>
    /// The mod's buffer roster and its review keys (Ctrl plus arrows), the Harkest Dungeon pattern:
    /// review lists for the information a focus announcement leaves out. So far one buffer, the
    /// focused control's own lines (its head, then one line per description part and detail); the
    /// game's own lists (the word log, the letter bag, the modifiers) join it as their screens land.
    /// Every buffer reads live on each keypress; a focus change re-homes review to the control's buffer
    /// and rewinds the control-fed ones, since a new control is new content. An empty buffer is skipped.
    /// </summary>
    internal static class Buffers
    {
        public static BufferManager Manager { get; private set; }
        public static BufferControls Controls { get; private set; }

        private static Buffer _ui;
        private static ControlId _homed;

        public static void Init()
        {
            Manager = new BufferManager();
            Controls = new BufferControls(Manager, (text, interrupt) => Speech.Say(text, interrupt: interrupt));

            _ui = Manager.Add(new Buffer(BufferKeys.Ui, () => Strings.BufferUi));
            _ui.SetSource(() => NodeLines.Lines(Navigation.FocusedNode));
        }

        /// <summary>Per frame: a focus change (by key or by the game moving things) re-homes review to
        /// the control's own buffer and rewinds the control-fed buffers.</summary>
        public static void Tick()
        {
            if (Manager == null) return;
            var id = Navigation.FocusedNode?.Id;
            if (Equals(id, _homed)) return;
            _homed = id;
            _ui.Reset();
            Manager.SetCurrent(BufferKeys.Ui);
        }
    }
}
