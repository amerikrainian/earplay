using System;
using System.Collections.Generic;
using System.Text;

namespace Earplay.Core.Strings
{
    /// <summary>
    /// Central table for text the MOD itself authors and speaks (never game content, which is read live
    /// and already localized). Every authored word lives in <see cref="Defaults"/> as a key and its
    /// English value; typed accessors read through the loaded translation, so a lang/&lt;language&gt;.txt
    /// file overrides any value at speak time. Word order lives in "{0}"-style templates, never in code.
    /// Values are spoken by a screen reader: terse, lowercase unless shown otherwise, no decorative
    /// punctuation. Keys follow the wotr-access locale vocabulary (role.*, state.*, nav.*, screen.*).
    /// </summary>
    public static class Strings
    {
        private static KeyValuePair<string, string> D(string key, string value)
            => new KeyValuePair<string, string>(key, value);

        /// <summary>Every authored string, in template order: the key a translation file addresses and
        /// its English default.</summary>
        internal static readonly KeyValuePair<string, string>[] Defaults =
        {
            // Spoken once when the mod finishes loading; {0} = the mod version.
            D("app.loaded", "Earplay {0} loaded"),
            // Spoken at launch instead when the feature module fails to load (the mod is then dead).
            D("app.module_failed", "Earplay features failed to load"),
            // Follows the loaded line when the newest release outranks the running build; {0} = that
            // newer version. Up to date (or ahead, a dev build) stays silent.
            D("app.update_available", "update {0} available"),
            // Spoken when the focus-mode toggle engages / releases our keyboard navigation.
            D("app.focus_on", "navigation on"),
            D("app.focus_off", "navigation off"),

            // Control role words, spoken after the label ("Play, button"). A list has no role word:
            // a context names its group by its label alone.
            D("role.button", "button"),
            D("role.toggle", "toggle"),
            D("role.slider", "slider"),
            D("role.radio button", "radio button"),
            D("role.combo box", "combo box"),
            D("role.tab", "tab"),
            D("role.key binding", "key binding"),
            D("role.item", "item"),
            D("role.heading", "heading"),
            D("role.table", "table"),
            D("role.link", "link"),
            D("role.edit", "edit"),
            D("role.dialog", "dialog"),

            // Control states.
            D("state.disabled", "disabled"),
            D("state.selected", "selected"),
            D("state.on", "on"),
            D("state.off", "off"),
            D("state.expanded", "expanded"),
            D("state.collapsed", "collapsed"),
            D("value.blank", "blank"),
            D("value.space", "space"),
            // A volume slider's value; {0} = a whole number from 0 to 100.
            D("value.percent", "{0}%"),
            D("edit.begin", "editing"),
            D("edit.selected", "{0} selected"),
            D("edit.unselected", "{0} unselected"),
            D("edit.deleted", "{0} deleted"),

            // Navigation feedback. {0} = index, {1} = count.
            D("nav.position", "{0} of {1}"),
            D("nav.no_tooltip", "no description"),
            D("nav.no_details", "no details"),
            // Drag (Backslash) and remove (Delete) with nothing under focus that takes them.
            D("drag.no_target", "nothing to drag"),
            D("delete.no_target", "nothing to remove"),
            D("nav.minimum", "minimum"),
            D("nav.maximum", "maximum"),
            // Buffer review (Ctrl plus arrows): the buffer names, the "name: line" readout on switching,
            // and the words when every buffer is empty.
            D("buffer.ui", "control"),
            D("buffer.none", "no buffer lines"),
            // {0} = the buffer's name, {1} = its current line.
            D("buffer.line", "{0}: {1}"),

            // Type-ahead search. {0} = the typed text.
            D("search.no_match", "no match for {0}"),
            D("search.cleared", "search cleared"),

            // Input action labels (for the key help). Short imperative phrases.
            D("bind.ui.up", "Navigate up"),
            D("bind.ui.down", "Navigate down"),
            D("bind.ui.left", "Navigate left"),
            D("bind.ui.right", "Navigate right"),
            D("bind.ui.next", "Next control group"),
            D("bind.ui.prev", "Previous control group"),
            D("bind.ui.activate", "Activate"),
            D("bind.ui.secondary", "Secondary action"),
            D("bind.ui.back", "Back"),
            D("bind.ui.home", "Jump to first"),
            D("bind.ui.end", "Jump to last"),
            D("bind.ui.tooltip", "Read description"),
            D("bind.ui.regionPrev", "Previous section"),
            D("bind.ui.regionNext", "Next section"),
            D("bind.ui.readFocus", "Read current control"),
            // Buffer review (Ctrl plus arrows): switch between the review lists, step through one.
            D("bind.buffer.next", "Next buffer"),
            D("bind.buffer.prev", "Previous buffer"),
            D("bind.buffer.line.next", "Next buffer line"),
            D("bind.buffer.line.prev", "Previous buffer line"),
            D("bind.mod.focus", "Toggle navigation"),
            D("bind.mod.reload", "Reload mod code"),
            D("bind.mod.menu", "Mod menu"),
            // The context-sensitive key help (F1): the keys that do something on this screen, on this
            // control. Enter on a row runs it.
            D("bind.mod.help", "Keys here"),

            // Screen names, spoken on entering a screen.
            D("screen.main_menu", "Main menu"),
            D("screen.help", "Keys here"),
            // The title scene's pages that draw no title of their own.
            D("screen.level_select", "Level select"),
            D("screen.consent", "Data collection"),
            D("screen.extras", "Extras"),
            // The title's caption-less icon buttons, and the options page's resolution dropdown, which
            // draws only its current value.
            D("title.options", "Options"),
            D("title.full_screen", "Full screen"),
            D("title.resolution", "Resolution"),
            // A mode tile on the level select: locked until Easy Mode is beaten; beaten (gold).
            D("level.locked", "locked"),
            D("level.completed", "completed"),
            // A row of the Extras stats: {0} = the stat's label, {1} = its value.
            D("extras.stat", "{0}: {1}"),

            // The round. Its three stops.
            D("round.board", "Board"),
            D("round.word", "Word"),
            D("round.status", "Status"),
            // A board cell: what it holds, then its column and row (1-based, row 1 at the top).
            // {0} = what it holds, {1} = column, {2} = row.
            D("cell.at", "{0}, {1} {2}"),
            D("cell.empty", "empty"),
            D("cell.locked_slot", "locked slot"),
            // A board tile that is in the word now; {0} = the tile.
            D("cell.in_word", "{0}, in word"),
            // A tile's points; {0} = the number (one key per form: the strings have no plurals).
            D("tile.point", "{0} point"),
            D("tile.points", "{0} points"),
            // What makes a tile special (the game describes each; these are their short names).
            D("tile.golden", "golden"),
            D("tile.diamond", "diamond"),
            D("tile.diamond_stored", "diamond, {0} stored"),
            D("tile.emerald", "emerald"),
            D("tile.potion", "potion"),
            D("tile.dot", "dot"),
            D("tile.glass", "glass"),
            D("tile.locked", "locked"),
            // A locked tile and the words left on its countdown; {0} = that count.
            D("tile.locked_count", "locked, {0} words"),
            D("tile.mandatory", "mandatory"),
            D("tile.stuck", "stuck"),
            // The odd tiles, which have no letter to read.
            D("tile.wildcard", "wildcard"),
            D("tile.plus", "plus"),
            D("tile.exclamation", "exclamation"),
            D("tile.mirror", "mirror"),
            // The tools the game names nowhere (Refresh, Shuffle, Sort, Letter Bag take the game's).
            D("tool.backspace", "Backspace"),
            D("tool.submit", "Submit"),
            D("tool.refresh", "Refresh"),
            D("tool.shuffle", "Shuffle"),
            D("tool.sort", "Sort"),
            D("tool.bag", "Letter Bag"),
            // {0} = refreshes left / tiles in the bag.
            D("tool.refreshes_left", "{0} left"),
            D("tool.bag_tiles", "{0} tiles"),

            D("slot.required", "required"),
            // A status line: {0} = its label, {1} = its value ("PLAYS: 10").
            D("status.pair", "{0}: {1}"),
            D("status.plays", "Plays"),
            // As it happens. {0} = a tile ("C").
            D("event.removed", "{0} removed"),
            D("event.no_tile", "no {0}"),
            D("event.cleared", "word cleared"),
            // {0} = the word as it stands after the gaps close.
            D("event.gaps_closed", "gaps closed, {0}"),
            // {0} = the word; {1} = its score.
            D("event.scored", "{0}: {1}"),
            // {0} = the word the game refused.
            D("event.rejected", "{0}: not a word"),
            D("event.round_complete", "round complete"),
            // The tiles a word's refill dealt; {0} = each with its coordinates, "E, 2 1; S, 3 2".
            D("event.new_tiles", "new: {0}"),
            D("event.shuffled", "shuffled"),
            D("event.sorted", "sorted"),
            // {0} = refreshes left.
            D("event.refreshed", "refreshed, {0} left"),
            // The letter bag: {0} = how many tiles.
            D("bag.remaining", "in the bag, {0}"),
            D("bag.spent", "spent this round, {0}"),
            D("bind.round.clear", "Clear the word"),
            D("bind.round.closegaps", "Close gaps"),
            D("bind.ui.delete", "Return the tile to the board"),

            // The mod's own menu (Ctrl+Shift+M).
            D("screen.mod_menu", "Earplay"),
            D("mod.settings", "Settings"),
            // The mod menu's list of every key the mod binds, whatever the screen.
            D("mod.key_help", "All keys"),
            D("mod.close", "Close"),
            D("mod.speak_positions", "Speak list positions"),
            D("mod.focus_on_launch", "Keyboard navigation on at launch"),
            // Whether the round's board cells end with their column and row.
            D("mod.speak_coordinates", "Speak board coordinates"),
            // {0} = the key category, one of the three names below.
            D("mod.key_category", "{0} keys"),
            // The key categories of the "All keys" list: live everywhere, on the focused screen, in a game.
            D("mod.category.global", "Global"),
            D("mod.category.ui", "UI"),
            D("mod.category.game", "Game"),
            D("help.none", "no keys here"),
            // {0} = what the key does, {1} = the keys bound to it.
            D("help.row", "{0}: {1}"),
        };

        private static readonly Dictionary<string, string> _defaults = BuildDefaults();
        private static Dictionary<string, string> _overrides = new Dictionary<string, string>();

        private static Dictionary<string, string> BuildDefaults()
        {
            var d = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var kv in Defaults) d[kv.Key] = kv.Value;
            return d;
        }

        /// <summary>The value for a key: the loaded translation, else the English default, else the
        /// key itself (a missing key is then audible rather than silent).</summary>
        public static string Get(string key)
        {
            if (_overrides.TryGetValue(key, out var o)) return o;
            if (_defaults.TryGetValue(key, out var v)) return v;
            CoreLog.Warning("Strings: missing key " + key);
            return key;
        }

        /// <summary>Whether a key exists in the defaults (for optional lookups like role words).</summary>
        public static bool Has(string key) => _defaults.ContainsKey(key) || _overrides.ContainsKey(key);

        /// <summary>The value for a key with "{0}"-style slots filled.</summary>
        public static string F(string key, params object[] args)
        {
            string template = Get(key);
            try { return string.Format(template, args); }
            catch (FormatException) { return template; }
        }

        /// <summary>Install a translation, replacing any previous one whole (a language switch never
        /// blends two files): "key=value" lines split at the first '=' ("#" comments and blanks
        /// ignored, a written-out "\n" a line break); missing keys fall back to English. Pass null to
        /// return to the defaults. What the file got wrong is set aside, never applied, and comes
        /// back in the report for the caller to log: a line without a key, a key this table does not
        /// define (a typo, a file for a newer mod), an empty value (it would silence information), a
        /// value with a slot its English has not ("{2}" where the code fills two: the line would be
        /// spoken as the raw template).</summary>
        public static TranslationReport LoadTranslation(IEnumerable<string> lines)
        {
            var report = new TranslationReport();
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            if (lines != null)
            {
                int n = 0;
                foreach (var raw in lines)
                {
                    n++;
                    if (raw == null) continue;
                    string line = raw.Trim().TrimStart('\uFEFF');
                    if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal)) continue;
                    int eq = line.IndexOf('=');
                    if (eq <= 0) { report.Malformed.Add(n); continue; }
                    string key = line.Substring(0, eq).Trim();
                    string value = line.Substring(eq + 1).Trim().Replace("\\n", "\n");
                    if (!_defaults.TryGetValue(key, out var english)) { report.UnknownKeys.Add(key); continue; }
                    if (value.Length == 0) { report.EmptyKeys.Add(key); continue; }
                    if (!SlotsFit(value, english)) { report.BadSlots.Add(key); continue; }
                    map[key] = value;
                }
            }
            _overrides = map;
            report.Applied = map.Count;
            return report;
        }

        // Whether a translated template formats with the arguments its English takes: every "{n}" of
        // it one the English has, and the braces well formed.
        public static bool SlotsFit(string value, string english)
        {
            int max = -1;
            foreach (int slot in Slots(english)) if (slot > max) max = slot;
            try { string.Format(value, new object[Math.Max(max + 1, 0)]); }
            catch (FormatException) { return false; }
            return true;
        }

        /// <summary>The "{n}" slots a template uses, for the translation checks.</summary>
        public static HashSet<int> Slots(string template)
        {
            var slots = new HashSet<int>();
            if (template == null) return slots;
            for (int i = 0; i < template.Length - 2; i++)
            {
                if (template[i] != '{' || !char.IsDigit(template[i + 1])) continue;
                int j = i + 1, slot = 0;
                while (j < template.Length && char.IsDigit(template[j])) slot = slot * 10 + (template[j++] - '0');
                if (j < template.Length && (template[j] == '}' || template[j] == ':' || template[j] == ',')) slots.Add(slot);
            }
            return slots;
        }

        /// <summary>Every key of the table, in template order (the translation tests walk it).</summary>
        public static IEnumerable<string> Keys
        {
            get { foreach (var kv in Defaults) yield return kv.Key; }
        }

        /// <summary>A key's English default, or null.</summary>
        public static string Default(string key) => _defaults.TryGetValue(key, out var v) ? v : null;

        private const string TemplateHeader =
            "# Earplay: the words the mod itself speaks (the game's own text is the game's).\n" +
            "# This file is the template, generated from the mod's strings table: copy it to\n" +
            "# strings.txt in the same folder and change the values. key=value, one per line; keep every\n" +
            "# {0} slot (their order is free); a missing key is spoken in English. Word Play itself is\n" +
            "# English only, so this is the mod's one override file, read at load and on every reload.\n";

        /// <summary>The template (lang/en.txt): every key with its English default, one per line,
        /// under a header that tells an editor what to do with it.</summary>
        public static string DumpTemplate()
        {
            var sb = new StringBuilder();
            sb.Append(TemplateHeader);
            foreach (var kv in Defaults)
                sb.Append(kv.Key).Append('=').Append(kv.Value.Replace("\n", "\\n")).Append('\n');
            return sb.ToString();
        }

        // ---- typed accessors ----

        public static string ModLoaded(string version) => F("app.loaded", version);
        public static string ModuleFailed => Get("app.module_failed");
        public static string UpdateAvailable(string version) => F("app.update_available", version);
        public static string FocusOn => Get("app.focus_on");
        public static string FocusOff => Get("app.focus_off");

        /// <summary>A control's role word ("button"), or null for a role with no word.</summary>
        public static string Role(string word) => Has("role." + word) ? Get("role." + word) : null;
        public static string RoleTable => Get("role.table");
        public static string RoleHeading => Get("role.heading");
        public static string RoleDialog => Get("role.dialog");

        public static string StateDisabled => Get("state.disabled");
        public static string StateSelected => Get("state.selected");
        public static string StateOn => Get("state.on");
        public static string StateOff => Get("state.off");
        public static string ExpandedState(bool expanded) => Get(expanded ? "state.expanded" : "state.collapsed");
        public static string ValueBlank => Get("value.blank");
        public static string ValueSpace => Get("value.space");
        public static string EditBegin => Get("edit.begin");
        public static string EditSelected(string text) => F("edit.selected", text);
        public static string EditUnselected(string text) => F("edit.unselected", text);
        public static string EditDeleted(string text) => F("edit.deleted", text);

        public static string Position(int index, int count) => F("nav.position", index, count);
        public static string NoTooltip => Get("nav.no_tooltip");
        public static string NoDetails => Get("nav.no_details");
        public static string NoDragTarget => Get("drag.no_target");
        public static string NoDeleteTarget => Get("delete.no_target");
        public static string Minimum => Get("nav.minimum");
        public static string Maximum => Get("nav.maximum");
        public static string BufferUi => Get("buffer.ui");
        public static string BufferNone => Get("buffer.none");
        public static string BufferLine(string name, string line) => F("buffer.line", name, line);

        public static string SearchNoMatch(string text) => F("search.no_match", text);
        public static string SearchCleared => Get("search.cleared");

        /// <summary>The spoken label of an input action ("bind.ui.down" -> "Navigate down"); the
        /// registration label when no entry exists.</summary>
        public static string BindLabel(string actionKey, string fallback)
            => Has("bind." + actionKey) ? Get("bind." + actionKey) : fallback;

        public static string ScreenMainMenu => Get("screen.main_menu");
        public static string RoundBoard => Get("round.board");
        public static string RoundWord => Get("round.word");
        public static string RoundStatus => Get("round.status");
        public static string CellAt(string what, int column, int row) => F("cell.at", what, column, row);
        public static string CellEmpty => Get("cell.empty");
        public static string CellLockedSlot => Get("cell.locked_slot");
        public static string TileInWord(string tile) => F("cell.in_word", tile);
        public static string Points(int n) => F(n == 1 ? "tile.point" : "tile.points", n);
        public static string TileGolden => Get("tile.golden");
        public static string TileDiamond => Get("tile.diamond");
        public static string TileDiamondStored(int n) => F("tile.diamond_stored", n);
        public static string TileEmerald => Get("tile.emerald");
        public static string TilePotion => Get("tile.potion");
        public static string TileDot => Get("tile.dot");
        public static string TileGlass => Get("tile.glass");
        public static string TileLockedPlain => Get("tile.locked");
        public static string TileLocked(int words) => F("tile.locked_count", words);
        public static string TileMandatory => Get("tile.mandatory");
        public static string TileStuck => Get("tile.stuck");
        public static string TileWildcard => Get("tile.wildcard");
        public static string TilePlus => Get("tile.plus");
        public static string TileExclamation => Get("tile.exclamation");
        public static string TileMirror => Get("tile.mirror");
        public static string ToolBackspace => Get("tool.backspace");
        public static string ToolSubmit => Get("tool.submit");
        public static string ToolRefresh => Get("tool.refresh");
        public static string ToolShuffle => Get("tool.shuffle");
        public static string ToolSort => Get("tool.sort");
        public static string ToolBag => Get("tool.bag");
        public static string RefreshesLeft(int n) => F("tool.refreshes_left", n);
        public static string BagTiles(int n) => F("tool.bag_tiles", n);
        public static string SlotRequired => Get("slot.required");
        public static string StatusPair(string label, string value) => F("status.pair", label, value);
        public static string StatusPlays => Get("status.plays");
        public static string TileRemoved(string tile) => F("event.removed", tile);
        public static string NoTile(string letter) => F("event.no_tile", letter);
        public static string WordCleared => Get("event.cleared");
        public static string GapsClosed(string word) => F("event.gaps_closed", word);
        public static string WordScored(string word, int score) => F("event.scored", word, score);
        public static string WordRejected(string word) => F("event.rejected", word);
        public static string RoundComplete => Get("event.round_complete");
        public static string NewTiles(string list) => F("event.new_tiles", list);
        public static string Shuffled => Get("event.shuffled");
        public static string Sorted => Get("event.sorted");
        public static string Refreshed(int left) => F("event.refreshed", left);
        public static string BagRemaining(int n) => F("bag.remaining", n);
        public static string BagSpent(int n) => F("bag.spent", n);
        public static string ScreenLevelSelect => Get("screen.level_select");
        public static string ScreenConsent => Get("screen.consent");
        public static string ScreenExtras => Get("screen.extras");
        public static string TitleOptions => Get("title.options");
        public static string TitleFullScreen => Get("title.full_screen");
        public static string TitleResolution => Get("title.resolution");
        public static string LevelLocked => Get("level.locked");
        public static string LevelCompleted => Get("level.completed");
        public static string StatLine(string label, string value) => F("extras.stat", label, value);
        public static string Percent(int value) => F("value.percent", value);
        public static string ScreenHelp => Get("screen.help");
        public static string ScreenModMenu => Get("screen.mod_menu");
        public static string ModSettings => Get("mod.settings");
        public static string ModKeyHelp => Get("mod.key_help");
        public static string ModClose => Get("mod.close");
        public static string ModSpeakPositions => Get("mod.speak_positions");
        public static string ModFocusOnLaunch => Get("mod.focus_on_launch");
        public static string ModSpeakCoordinates => Get("mod.speak_coordinates");
        public static string HelpNone => Get("help.none");
        public static string HelpRow(string label, string keys) => F("help.row", label, keys);

        /// <summary>"Global keys": a category of the "All keys" list, by its InputCategory name.</summary>
        public static string ModKeyCategory(string category)
        {
            string name = "mod.category." + (category ?? "").ToLowerInvariant();
            return F("mod.key_category", Has(name) ? Get(name) : category);
        }
    }
}
