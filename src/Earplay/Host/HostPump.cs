using System;
using Earplay.Dev;
using Earplay.Modularity;
using UnityEngine;

namespace Earplay.Host
{
    /// <summary>
    /// The host's one MonoBehaviour, on a GameObject of its own that survives scene loads. It lives in
    /// the host (a component type of a byte-loaded module would be a new type every reload, and Unity
    /// keeps component types for the process). Each frame it pumps the dev
    /// server's job queue, drives the current feature module's per-frame work, and watches for the F6
    /// reload hotkey. The module is read fresh from the loader each frame so a reload swaps it in with
    /// no further wiring.
    /// </summary>
    public sealed class HostPump : MonoBehaviour
    {
#if DEBUG
        internal static DevServer DevServer;
#endif
        internal static ModuleLoader Loader;

        private void Update()
        {
#if DEBUG
            try
            {
                DevServer?.Pump();
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning("DevServer.Pump: " + ex);
            }
#endif

            try
            {
                Loader?.Module?.Tick();
            }
            catch (Exception ex)
            {
                // Full exception (with stack); a per-frame Tick failure is otherwise unlocatable.
                Plugin.Log?.LogWarning("Module.Tick: " + ex);
            }

#if DEBUG
            if (UnityEngine.Input.GetKeyDown(KeyCode.F6))
            {
                Plugin.Log?.LogInfo("F6: reloading module");
                // Through the dev server so the eval REPL is reset to the new module's types too,
                // matching POST /reload.
                DevServer?.ReloadFromHost();
            }
#endif
        }
    }
}
