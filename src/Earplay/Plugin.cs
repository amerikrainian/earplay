using System.IO;
using BepInEx;
using BepInEx.Logging;
using Earplay.Contracts;
using Earplay.Host;
using Earplay.Modularity;
using Earplay.Speech;
using UnityEngine;

namespace Earplay
{
    /// <summary>
    /// The permanent host: only what can never reload. Stands up Prism, the speech pipeline, the settings
    /// store, the pump, and the module loader, then hands per-frame work to the
    /// reloadable side (Core + Module). No feature logic lives here.
    /// </summary>
    [BepInPlugin(Guid, Name, BuildVersion.Value)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid = "com.amerikranian.earplay";
        public const string Name = "Earplay";
        public const string Version = BuildVersion.Value;

        internal static ManualLogSource Log;

        private PrismBackend _prism;
        private ModuleLoader _loader;

        private void Awake()
        {
            Log = Logger;
            Log.LogInfo($"{Name} {Version} loading");

            // Headless runs set EARPLAY_NO_SPEECH=1 so an unattended session does not depend on a
            // running screen reader: Prism init is skipped.
            bool muteSpeech = System.Environment.GetEnvironmentVariable("EARPLAY_NO_SPEECH") == "1";
            _prism = new PrismBackend(Log);
            if (muteSpeech)
                Log.LogInfo("Speech muted (EARPLAY_NO_SPEECH=1)");
            else
                _prism.Initialize();

            var speech = new SpeechPipeline(_prism) { Muted = muteSpeech };

            // Settings persist through our BepInEx config file and are owned here (permanent) so they
            // survive a module hot-reload; the module reads them through IModHost.Settings.
            var settings = new ModSettings(new BepInExSettingsStore(Config));

            string pluginDir = Path.GetDirectoryName(typeof(Plugin).Assembly.Location);
            var host = new ModHost(Log, speech, settings, pluginDir);

            _loader = new ModuleLoader(
                Path.Combine(pluginDir, "Earplay.Core.dll"),
                Path.Combine(pluginDir, "Earplay.Module.dll"),
                host, Log);

            // The "loaded" line is the module's: it has the strings table. The module carries every
            // feature; if it fails to load the player would otherwise get silence with no signal, so
            // say so out loud (the cause is in the log).
            if (!_loader.Load())
                speech.Speak(Name + " features failed to load", interrupt: false);

            // The pump lives on a GameObject of its own that survives scene loads, rather than on
            // BepInEx's manager object, so nothing the game does to that object stops the mod.
            var pumpObject = new GameObject("Earplay.HostPump");
            pumpObject.hideFlags = HideFlags.HideAndDontSave;
            DontDestroyOnLoad(pumpObject);
            HostPump.Loader = _loader;
            pumpObject.AddComponent<HostPump>();

            Log.LogInfo($"{Name} loaded");
        }
    }
}
