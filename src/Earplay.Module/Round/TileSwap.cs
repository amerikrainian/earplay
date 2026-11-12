using Earplay.Core;
using Earplay.Core.Strings;
using Earplay.Module.UI;

namespace Earplay.Module.Round
{
    /// <summary>
    /// Moving a tile by keyboard (Space): the first press on a tile picks it up (the mod only notes
    /// it: the tile stays put, and typing goes on working); a press on another board tile or word slot
    /// drops it there through the game's own drop, the same calls its gamepad drag makes
    /// (<c>TileDrag.OnBeginControlDrag</c> then <c>DropTile(target holder)</c>), so every rule of the
    /// mouse drag holds: two board tiles swap places, a word slot takes the tile (an occupied one
    /// swaps, sending the occupant back), a word tile dropped on the board goes back to its own spot, a
    /// locked or stuck tile refuses. A press on the held tile itself, or Escape, puts it back down.
    /// The word row's own announcements stand down for the move; its result is one line, read once
    /// the tiles have settled: "C swapped with A", "C moved", or "can't move C there" (the game sent
    /// it back).
    /// </summary>
    internal static class TileSwap
    {
        private static TileScript _held;

        /// <summary>The tile picked up, or null (Unity's null: a tile destroyed meanwhile is none).</summary>
        public static TileScript Held => _held != null ? _held : null;

        public static bool Holding => Held != null;

        /// <summary>Space on a board cell's or word slot's holder.</summary>
        public static void Press(TileHolderScript target, TileScript targetTile)
        {
            if (!Round.CanInteract || target == null) return;
            var held = Held;
            if (held == null)
            {
                PickUp(targetTile);
                return;
            }
            if (ReferenceEquals(targetTile, held))
            {
                Cancel();
                return;
            }
            Drop(held, target, targetTile);
        }

        private static void PickUp(TileScript tile)
        {
            if (tile == null || tile.fullySpent)
            {
                Speech.Say(Strings.NoDragTarget, interrupt: true);
                return;
            }
            string name = Round.TileName(tile);
            if (tile.myType == SpecialTileManager.TileTypes.Locked || tile.stuckTile || tile.transparent || tile.tileIsMoving)
            {
                Speech.Say(Strings.CantPickUp(name), interrupt: true);
                return;
            }
            _held = tile;
            Speech.Say(Strings.PickedUp(name), interrupt: true);
        }

        /// <summary>Put the held tile back down (Escape, or Space on it).</summary>
        public static void Cancel()
        {
            var held = Held;
            _held = null;
            if (held != null) Speech.Say(Strings.PutDown(Round.TileName(held)), interrupt: true);
        }

        /// <summary>Drop the hold silently (the round left the screen, a word was submitted).</summary>
        public static void Reset() => _held = null;

        private static void Drop(TileScript held, TileHolderScript target, TileScript targetTile)
        {
            _held = null;
            string name = Round.TileName(held);
            // The tile already there and still in place, the one a move swaps with.
            string other = targetTile != null && !targetTile.fullySpent ? Round.TileName(targetTile) : null;
            var parentBefore = held.transform.parent;
            RoundEvents.QuietFor(40);
            try
            {
                held.dragScript.OnBeginControlDrag();
                held.dragScript.DropTile(target.gameObject);
            }
            catch (System.Exception e)
            {
                CoreLog.Warning("TileSwap: drop failed: " + e);
                return;
            }
            Later.Frames(25, () =>
            {
                if (held == null) return;
                if (held.transform.parent == parentBefore)
                    Speech.Say(Strings.CantMoveThere(name), interrupt: true);
                else if (other != null)
                    Speech.Say(Strings.Swapped(name, other), interrupt: true);
                else
                    Speech.Say(Strings.Moved(name), interrupt: true);
            }, "tile swap");
        }
    }
}
