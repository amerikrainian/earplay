using System;
using Earplay.Core;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Earplay.Module.UI
{
    /// <summary>
    /// When active, our navigation owns the keyboard. Two things make that true:
    /// <list type="bullet">
    /// <item>The uGUI EventSystem stops sending navigation events and its selection is cleared, so
    /// arrows and Enter cannot also move or submit the game's own selection.</item>
    /// <item>The keyboard is disabled as an Input System device, so none of the game's actions (its
    /// PlayerInput's navigate, submit and back, the title's Escape) hear a key. Our own keys are polled through the legacy input path, which this does not touch.
    /// Whenever the Input System enables a keyboard again (the window regaining focus, a reload's old
    /// generation releasing it, a keyboard plugged in), its device-change event lets us disable it
    /// once more: no scanning, no per-frame checks.</item>
    /// </list>
    /// Fully reversible: turning focus mode off re-enables the keyboard and the EventSystem's
    /// navigation. A fresh EventSystem (Unity builds one per scene, with no event for it) is noticed
    /// by a per-frame instance compare and suppressed on sight.
    /// </summary>
    public static class FocusMode
    {
        private static bool _active;
        private static EventSystem _applied; // the EventSystem instance the suppression was applied to
        private static Action<InputDevice, InputDeviceChange> _deviceHook;

        public static bool Active => _active;

        public static void Toggle() => Set(!_active);

        public static void Set(bool on)
        {
            if (on == _active) return;
            _active = on;
            if (on)
            {
                Apply(EventSystem.current);
                HookDevices();
                LockKeyboards();
            }
            else
            {
                Release();
                UnlockKeyboards();
            }
        }

        /// <summary>Per-frame: apply the EventSystem suppression to a fresh EventSystem (the game builds
        /// one per scene, and Unity offers no event for it). Nothing else is reasserted: a reload's old
        /// generation no longer restores what its successor owns (see <see cref="Shutdown"/>).</summary>
        public static void Tick()
        {
            if (!_active) return;
            var es = EventSystem.current;
            if (es != null && !ReferenceEquals(es, _applied)) Apply(es);
        }

        /// <summary>Module teardown. With <paramref name="restore"/> the game gets its keyboard and
        /// navigation events back (a shutdown); without it only our hooks are dropped, because a newer
        /// generation has already taken over (a reload).</summary>
        public static void Shutdown(bool restore)
        {
            if (restore) Set(false);
            else _active = false;
            if (_deviceHook == null) return;
            try { InputSystem.onDeviceChange -= _deviceHook; }
            catch (Exception e) { CoreLog.Warning("FocusMode: unhooking device changes failed: " + e.Message); }
            _deviceHook = null;
        }

        // ---- the EventSystem ----

        private static void Apply(EventSystem es)
        {
            _applied = es;
            if (es == null)
            {
                CoreLog.Info("FocusMode: no EventSystem yet; will engage when one appears.");
                return;
            }
            es.sendNavigationEvents = false;
            es.SetSelectedGameObject(null);
        }

        private static void Release()
        {
            var es = _applied ?? EventSystem.current;
            _applied = null;
            if (es != null) es.sendNavigationEvents = true;
        }

        // ---- the keyboard device ----

        private static void HookDevices()
        {
            if (_deviceHook != null) return;
            try
            {
                _deviceHook = OnDeviceChange;
                InputSystem.onDeviceChange += _deviceHook;
            }
            catch (Exception e)
            {
                CoreLog.Warning("FocusMode: hooking device changes failed: " + e.Message);
                _deviceHook = null;
            }
        }

        // A keyboard coming (back) to life while we own the keys: take it again.
        private static void OnDeviceChange(InputDevice device, InputDeviceChange change)
        {
            try
            {
                if (!_active || device == null) return;
                if (change != InputDeviceChange.Added && change != InputDeviceChange.Enabled) return;
                if (!(device is Keyboard)) return;
                InputSystem.DisableDevice(device, false);
            }
            catch (Exception e)
            {
                CoreLog.Warning("FocusMode: device change handling failed: " + e.Message);
            }
        }

        private static void LockKeyboards()
        {
            try
            {
                foreach (var keyboard in Keyboards())
                    if (keyboard.enabled) InputSystem.DisableDevice(keyboard, false);
            }
            catch (Exception e)
            {
                CoreLog.Warning("FocusMode: disabling the keyboard failed: " + e.Message);
            }
        }

        private static void UnlockKeyboards()
        {
            try
            {
                foreach (var keyboard in Keyboards())
                    if (!keyboard.enabled) InputSystem.EnableDevice(keyboard);
            }
            catch (Exception e)
            {
                CoreLog.Warning("FocusMode: enabling the keyboard failed: " + e.Message);
            }
        }

        private static System.Collections.Generic.List<Keyboard> Keyboards()
        {
            var list = new System.Collections.Generic.List<Keyboard>();
            var devices = InputSystem.devices;
            for (int i = 0; i < devices.Count; i++)
            {
                if (devices[i] is Keyboard keyboard) list.Add(keyboard);
            }
            return list;
        }
    }
}
