using System;
using Earplay.Modularity;
using UnityEngine;

namespace Earplay.Host
{
    /// <summary>
    /// The host's one MonoBehaviour, on a GameObject of its own that survives scene loads. It lives in
    /// the host (a component type of a byte-loaded module would be a new type every reload, and Unity
    /// keeps component types for the process). Each frame it drives the current feature module's
    /// per-frame work. The module is read fresh from the loader each frame so a reload swaps it in
    /// with no further wiring.
    /// </summary>
    public sealed class HostPump : MonoBehaviour
    {
        internal static ModuleLoader Loader;

        private void Update()
        {
            try
            {
                Loader?.Module?.Tick();
            }
            catch (Exception ex)
            {
                // Full exception (with stack); a per-frame Tick failure is otherwise unlocatable.
                Plugin.Log?.LogWarning("Module.Tick: " + ex);
            }
        }
    }
}
