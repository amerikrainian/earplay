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
    /// The level select (Play on the main menu): the mode tiles in their two groups, each group named
    /// by its own label ("STRATEGIC": Easy, Normal, Hard, Legendary, Marathon, and Ultramarathon once
    /// the five are beaten; "CASUAL": Quick Play), then the page's Play and Back buttons. A tile is a
    /// letter on the screen; it reads by its mode's name from <c>LevelSelectScript.levelNames</c>, the
    /// list its index in <c>pickerTiles</c> addresses (the same index the tile's click hands to
    /// <c>ChangeDetails</c>). Each group is a row, as drawn: Left/Right within it, Up/Down between
    /// the groups and on to Play and Back. Landing on a tile selects it, as a click does (a locked one
    /// too: the game then greys Play out), so the details panel follows focus and the tile reads its
    /// description and record ("Not Yet Played", "Complete Easy Mode to Unlock") after its name. Since
    /// the tile landed on is always the selected one, a tile says no "selected", as a tab does not; a
    /// locked tile says locked, and a beaten one (its gold overlay) says completed. Play starts the
    /// selected mode. Escape presses Back.
    /// </summary>
    public sealed class LevelSelectScreen : Screen
    {
        public override string Key => "title.levels";
        public override string ScreenName => Strings.ScreenLevelSelect;

        public override bool IsActive() => TitlePages.Showing(TitleControl.TitleControlZone.LevelSelect);

        // The tile last landed on in each group (by the group's object name): Up/Down into a row
        // return to it. The selected tile cannot say this, since landing on another row's tile
        // selects that one. Navigation memory, not game state.
        private readonly Dictionary<string, int> _lastInRow = new Dictionary<string, int>();

        private static LevelSelectScript Script()
        {
            var m = TitlePages.Manager;
            return m != null && m.levelSelectGO != null ? m.levelSelectGO.GetComponent<LevelSelectScript>() : null;
        }

        public override void Build(GraphBuilder b)
        {
            var script = Script();
            if (script == null) return;
            var tiles = Private.Get<List<GameObject>>(script, "pickerTiles");
            var gold = Private.Get<List<GameObject>>(script, "goldenOverlays");
            var locks = Private.Get<List<GameObject>>(script, "lockIcons");
            var description = Private.Get<TMP_Text>(script, "detailDescription");
            var record = Private.Get<TMP_Text>(script, "detailBestScore");
            if (tiles == null) return;

            b.BeginStop("levels");
            // The groups in the page's order, each the tiles under it. A tile appears in pickerTiles
            // more than once (index 0 is an unused duplicate of Easy): the first index that is not the
            // unused slot wins.
            var picker = script.transform.Find("Picker");
            if (picker != null)
            {
                for (int g = 0; g < picker.childCount; g++)
                {
                    var group = picker.GetChild(g);
                    if (!group.gameObject.activeInHierarchy) continue;
                    var row = new List<(Button tile, int id)>();
                    foreach (var tile in group.GetComponentsInChildren<Button>(false))
                    {
                        int id = IndexOf(tiles, tile.gameObject);
                        if (id > 0) row.Add((tile, id));
                    }
                    if (row.Count == 0) continue;
                    var caption = FirstGroupLabel(group);
                    b.PushContext(caption != null ? caption.text : group.name);
                    // A group is a row, as drawn: Left/Right walk its tiles, Up/Down cross to the
                    // next group (or Play), entering a row on the tile last landed on there, else
                    // its selected one, else its first.
                    string rowName = group.name;
                    b.StartRow(entry: () =>
                    {
                        int target = _lastInRow.TryGetValue(rowName, out int last) ? last : Private.Get<int>(script, "currentID");
                        return row.FindIndex(t => t.id == target);
                    });
                    foreach (var (tile, id) in row)
                    {
                        var vt = Tile(script, tile, id, gold, locks, description, record);
                        var select = vt.OnFocus;
                        vt.OnFocus = () => { _lastInRow[rowName] = id; select(); };
                        b.AddItem(ControlId.Structural("levels:" + id), vt);
                    }
                    b.EndRow();
                    b.PopContext();
                }
            }

            var play = Private.Get<Button>(script, "playButton");
            TitlePages.AddButton(b, "levels:play", play);
            TitlePages.AddButton(b, "levels:back", TitlePages.BackButton(script.gameObject));
        }

        private static NodeVtable Tile(LevelSelectScript script, Button tile, int id, List<GameObject> gold,
            List<GameObject> locks, TMP_Text description, TMP_Text record)
        {
            System.Func<string> name = () => id < script.levelNames.Count ? script.levelNames[id] : tile.gameObject.name;
            System.Func<bool> selected = () => Private.Get<int>(script, "currentID") == id;
            return new NodeVtable
            {
                ControlType = ControlTypes.RadioButton,
                Announcements = new List<NodeAnnouncement>
                {
                    GameNodes.LabelPart(name),
                    new NodeAnnouncement(() => Shown(locks, id) ? Strings.LevelLocked : null, kind: AnnouncementKinds.Value),
                    new NodeAnnouncement(() => Shown(gold, id) ? Strings.LevelCompleted : null, kind: AnnouncementKinds.Value),
                    // The details panel describes the selected mode, which landing has just made this one.
                    GameNodes.TooltipPart(() => selected() && description != null ? description.text : null),
                    GameNodes.TooltipPart(() => selected() && record != null ? record.text : null),
                },
                SearchText = name,
                OnFocus = () => { if (!selected()) tile.onClick.Invoke(); },
                OnActivate = () => { if (!selected()) tile.onClick.Invoke(); },
            };
        }

        private static bool Shown(List<GameObject> list, int id)
            => list != null && id < list.Count && list[id] != null && list[id].activeInHierarchy;

        private static int IndexOf(List<GameObject> tiles, GameObject tile)
        {
            for (int i = 1; i < tiles.Count; i++)
                if (tiles[i] == tile) return i;
            return -1;
        }

        // A group's caption: the TMP text under its "Label - ..." child.
        private static TMP_Text FirstGroupLabel(Transform group)
        {
            for (int i = 0; i < group.childCount; i++)
            {
                var child = group.GetChild(i);
                if (!child.name.StartsWith("Label", System.StringComparison.Ordinal)) continue;
                var text = child.GetComponentInChildren<TMP_Text>(false);
                if (text != null && !string.IsNullOrWhiteSpace(text.text)) return text;
            }
            return null;
        }

        public override IEnumerable<ElementAction> GetActions()
        {
            yield return new ElementAction(ActionIds.Back, Strings.Get("bind.ui.back"), _ =>
            {
                var script = Script();
                if (script != null) TitlePages.Back(script.gameObject);
            });
        }
    }
}
