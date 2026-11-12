namespace Earplay.Module
{
    /// <summary>
    /// The mod's settings that only the module reads, keyed straight into the host's store (the
    /// BepInEx config) so they persist like the host's own but need no host change: a new one is a
    /// module reload away. Read through the store on every access, never cached.
    /// </summary>
    internal static class ModuleSettings
    {
        /// <summary>Whether a board cell ends with its coordinates ("C, 3 points, 4 1"). On by default.</summary>
        public static bool SpeakCoordinates
        {
            get => ModuleMain.Host == null || ModuleMain.Host.Settings.Store.GetBool("speak_coordinates", true);
            set { if (ModuleMain.Host != null) ModuleMain.Host.Settings.Store.SetBool("speak_coordinates", value); }
        }
    }
}
