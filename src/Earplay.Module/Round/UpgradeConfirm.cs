using Earplay.Core;
using Earplay.Module.UI;
using HarmonyLib;

namespace Earplay.Module.Round
{
    /// <summary>
    /// The result of using a tile upgrade, by any route (Enter on the upgrade then on a tile, Space
    /// dropping a tile on it, the mouse): every route ends in <c>UpgradeStubScript.UpgradeTile</c>,
    /// which pauses interaction, transforms the tile, puts it back where it came from (the board spot
    /// or its word slot) and only then resumes. The hook notes the tile and its board spot, waits for
    /// interaction to resume, and re-reads the tile as landing on it reads it ("A, 1 point, diamond",
    /// with its coordinates on the board). A tile picker upgrade (Swap Tile) opens the game's letter
    /// picker instead, and is not read here.
    /// </summary>
    internal static class UpgradeConfirm
    {
        /// <summary>The tile last sent to an upgrade, until its result is read (TileSwap watches it to
        /// tell a refused drop from an accepted one).</summary>
        public static TileScript Pending { get; private set; }

        private const int MaxTries = 100; // about 5 s at 3 frames a try

        [HarmonyPatch(typeof(UpgradeStubScript), nameof(UpgradeStubScript.UpgradeTile))]
        private static class Applied
        {
            private static void Prefix(TileScript myTile, bool openTilePicker)
            {
                if (myTile == null) return;
                Pending = myTile;
                if (openTilePicker) return;
                var spot = myTile.myHolder;
                Later.Frames(10, () => WaitThenRead(myTile, spot, MaxTries), "upgrade result");
            }
        }

        private static void WaitThenRead(TileScript tile, TileHolderScript spot, int tries)
        {
            if (!Round.CanInteract || (tile != null && tile.tileIsMoving))
            {
                if (tries > 0) Later.Frames(3, () => WaitThenRead(tile, spot, tries - 1), "upgrade result");
                return;
            }
            Pending = null;
            string line = null;
            if (tile != null && tile.tileIsInSubmissionQueue) line = Round.TileLine(tile);
            else if (spot != null)
                foreach (var row in Round.Board())
                    foreach (var cell in row)
                        if (cell.Holder == spot) line = Round.CellLine(cell);
            if (line != null) Speech.Say(line, interrupt: true);
        }
    }
}
