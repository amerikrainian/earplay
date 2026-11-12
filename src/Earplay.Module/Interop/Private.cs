using System;
using System.Collections.Generic;
using System.Reflection;
using Earplay.Core;

namespace Earplay.Module.Interop
{
    /// <summary>
    /// Reads the game's private fields. On Mono a <c>[SerializeField] private</c> field is just a
    /// private field (no interop proxy exposes it), and most of what a screen needs lives in one:
    /// <c>TitleScreenManager.controlScript</c>, <c>LevelSelectScript.levelNames</c>. Each field is
    /// looked up once per generation and cached; a name the game no longer has logs once and reads as
    /// the default, so a game update that renames a field is loud in the log and quiet in play.
    /// </summary>
    internal static class Private
    {
        private static readonly Dictionary<(Type, string), FieldInfo> Fields = new Dictionary<(Type, string), FieldInfo>();
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        /// <summary>The value of <paramref name="name"/> on <paramref name="target"/>, or default when
        /// the target is null or the field is missing or of another type.</summary>
        public static T Get<T>(object target, string name)
        {
            if (target == null) return default;
            var field = Find(target.GetType(), name);
            if (field == null) return default;
            return field.GetValue(target) is T value ? value : default;
        }

        private static FieldInfo Find(Type type, string name)
        {
            var key = (type, name);
            if (Fields.TryGetValue(key, out var field)) return field;
            for (var t = type; t != null && field == null; t = t.BaseType)
                field = t.GetField(name, Flags | BindingFlags.DeclaredOnly);
            if (field == null) CoreLog.Warning("Private: " + type.Name + " has no field " + name);
            Fields[key] = field;
            return field;
        }
    }
}
