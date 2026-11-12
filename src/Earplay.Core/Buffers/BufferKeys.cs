namespace Earplay.Core.Buffers
{
    /// <summary>
    /// The registered buffer keys, in cycling order. The ui buffer carries the focused control's own
    /// lines (<see cref="NodeLines"/>). A buffer the focused control fills is keyed here too and
    /// answered through <see cref="Graph.NodeVtable.SideLines"/>; one that reads the game itself
    /// (a log, the board) sets its own source in the module.
    /// </summary>
    public static class BufferKeys
    {
        public const string Ui = "ui";
    }
}
