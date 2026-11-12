using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Earplay.Modularity;
using UnityEngine;

namespace Earplay.Dev
{
    /// <summary>
    /// Wraps the Roslyn scripting API so the dev driver can POST arbitrary C# and run it against the live
    /// game. State persists across calls (usings, variables defined in one eval are visible to the next),
    /// so the session behaves like a REPL. Evaluated code references every loaded, disk-backed assembly,
    /// every DLL of the game's Managed folder (Assembly-CSharp and the Unity modules, loaded or not),
    /// and the live generation's Core and Module from their images (byte-loaded, so they have no
    /// file), so a script can call into the mod's own types: <c>Earplay.Module.ModuleMain.Host</c>.
    /// A reload resets the session, since its types belong to the generation it compiled against.
    ///
    /// MUST be used from the Unity main thread: evaluated code routinely touches Unity objects.
    /// </summary>
    internal sealed class CSharpEvaluator
    {
        private readonly ModuleLoader _loader;
        private ScriptState<object> _state;
        private ScriptOptions _options;

        public CSharpEvaluator(ModuleLoader loader) => _loader = loader;

        /// <summary>Drop REPL state so the next Eval rebuilds it.</summary>
        public void Reset()
        {
            _state = null;
            _options = null;
        }

        private void Initialize()
        {
            var refs = new List<MetadataReference>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            void AddFile(string path)
            {
                // One reference per assembly name: the Managed folder and the loaded set overlap.
                if (string.IsNullOrEmpty(path) || !seen.Add(Path.GetFileName(path)))
                    return;
                try { refs.Add(MetadataReference.CreateFromFile(path)); }
                catch { }
            }

            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (asm.IsDynamic)
                    continue;
                string location;
                try { location = asm.Location; }
                catch { continue; }
                AddFile(location);
            }

            // The game's own folder: Assembly-CSharp, its plugins and every Unity module, including
            // the ones nothing has loaded yet when the first eval runs.
            string managed = Path.Combine(Application.dataPath, "Managed");
            if (Directory.Exists(managed))
                foreach (string dll in Directory.GetFiles(managed, "*.dll"))
                    AddFile(dll);

            if (_loader.CoreImage != null)
                refs.Add(MetadataReference.CreateFromImage(_loader.CoreImage));
            if (_loader.ModuleImage != null)
                refs.Add(MetadataReference.CreateFromImage(_loader.ModuleImage));

            _options = ScriptOptions.Default
                .WithReferences(refs)
                .WithImports("System", "System.Linq", "System.Reflection",
                    "System.Collections.Generic", "UnityEngine");
        }

        /// <summary>Compile a bool expression into a reusable delegate for the /wait endpoint, in the
        /// SAME session as /eval. Returns null on success with <paramref name="predicate"/> set, else
        /// the compile/exception text.</summary>
        public string CompilePredicate(string expression, out Func<bool> predicate)
        {
            predicate = null;
            if (_options == null)
                Initialize();
            try
            {
                _state = _state == null
                    ? CSharpScript.RunAsync("(System.Func<bool>)(() => (" + expression + "))", _options).GetAwaiter().GetResult()
                    : _state.ContinueWithAsync("(System.Func<bool>)(() => (" + expression + "))", _options).GetAwaiter().GetResult();
                predicate = _state.ReturnValue as Func<bool>;
                return predicate != null ? null : "[error] expression did not yield a bool predicate\n";
            }
            catch (CompilationErrorException e)
            {
                return "[compile] " + string.Join("\n", e.Diagnostics) + "\n";
            }
            catch (Exception e)
            {
                return "[exception] " + e + "\n";
            }
        }

        /// <summary>Compile and run <paramref name="code"/>; return output + result/errors.</summary>
        public string Eval(string code)
        {
            if (_options == null)
                Initialize();

            var output = new StringWriter();
            TextWriter origOut = Console.Out;
            TextWriter origErr = Console.Error;

            object value = null;
            bool hasValue = false;
            Exception thrown = null;

            Console.SetOut(output);
            Console.SetError(output);
            try
            {
                _state = _state == null
                    ? CSharpScript.RunAsync(code, _options).GetAwaiter().GetResult()
                    : _state.ContinueWithAsync(code, _options).GetAwaiter().GetResult();
                if (_state.ReturnValue != null)
                {
                    value = _state.ReturnValue;
                    hasValue = true;
                }
            }
            catch (Exception e)
            {
                thrown = e;
            }
            finally
            {
                Console.SetOut(origOut);
                Console.SetError(origErr);
            }

            var sb = new StringBuilder();
            string captured = output.ToString();
            if (captured.Length > 0)
            {
                sb.Append(captured);
                if (!captured.EndsWith("\n"))
                    sb.Append('\n');
            }
            if (thrown is CompilationErrorException compileError)
                sb.Append("[compile] ").Append(string.Join("\n", compileError.Diagnostics)).Append('\n');
            else if (thrown != null)
                sb.Append("[exception] ").Append(thrown).Append('\n');
            if (hasValue)
                sb.Append("=> ").Append(value).Append('\n');
            if (sb.Length == 0)
                sb.Append("(ok)\n");
            return sb.ToString();
        }
    }
}
