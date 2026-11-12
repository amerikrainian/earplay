using System;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx.Logging;
using Earplay.Contracts;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Earplay.Modularity
{
    /// <summary>
    /// Loads and reloads the feature side (Core, then Module) without restarting the game. Both DLLs
    /// are read into memory and loaded from their bytes (never from their paths), so the files on disk
    /// stay unlocked and dotnet build can overwrite them while the game runs.
    ///
    /// The game runs on Unity's Mono, which has no collectible load contexts: every generation is a
    /// fresh pair of assemblies in the one AppDomain, and the old pair stays loaded (a few hundred KB a
    /// reload, gone at exit). Mono binds a reference by NAME to whatever assembly of that name it has
    /// already loaded, and BepInEx's own resolver would load a same-named DLL from the plugin folder by
    /// path, so before loading, each generation's pair is renamed with Mono.Cecil (BepInEx ships it):
    /// "Earplay.Core" becomes "Earplay.Core.g3", "Earplay.Module" becomes "Earplay.Module.g3", and the
    /// module's reference to Core is rewritten to match. The names are unique per generation, so a
    /// reference can only bind to this generation's copy, which <see cref="Resolve"/> hands back.
    /// Namespaces and type names are untouched. Every other dependency (Contracts, Unity, the game,
    /// BepInEx, Harmony) binds to its one already-loaded copy, which keeps the Contracts type identity
    /// stable across the boundary. The host never references Core at compile time.
    /// </summary>
    internal sealed class ModuleLoader
    {
        private const string CoreName = "Earplay.Core";
        private const string ModuleName = "Earplay.Module";

        private readonly string _corePath;
        private readonly string _modulePath;
        private readonly ModHost _host;
        private readonly ManualLogSource _log;

        // The generation being loaded or live, for the resolve hook. Written on the main thread only.
        private Assembly _core;
        private Assembly _module;
        private Assembly _candidateCore;
        private Assembly _candidateModule;

        public ModuleLoader(string corePath, string modulePath, ModHost host, ManualLogSource log)
        {
            _corePath = corePath;
            _modulePath = modulePath;
            _host = host;
            _log = log;
            AppDomain.CurrentDomain.AssemblyResolve += Resolve;
        }

        /// <summary>The live module instance, or null if no load has succeeded. Read fresh each frame.</summary>
        public IModModule Module { get; private set; }

        /// <summary>How many loads have succeeded this process (1 = the boot load; each successful
        /// reload increments).</summary>
        public int Generation { get; private set; }

        public string CorePath => _corePath;
        public string ModulePath => _modulePath;

        /// <summary>The live generation's (renamed) Core and Module image bytes, for the dev REPL to
        /// compile against: a byte-loaded assembly has no file for Roslyn to reference.</summary>
        public byte[] CoreImage { get; private set; }
        public byte[] ModuleImage { get; private set; }

        /// <summary>Load Core + Module from their current bytes. Returns false (and logs) on any failure.</summary>
        public bool Load()
        {
            int gen = Generation + 1;
            try
            {
                if (!File.Exists(_corePath))
                {
                    _log.LogError("ModuleLoader: Core DLL not found at " + _corePath);
                    return false;
                }
                if (!File.Exists(_modulePath))
                {
                    _log.LogError("ModuleLoader: module DLL not found at " + _modulePath);
                    return false;
                }

                string suffix = ".g" + gen;
                byte[] coreImage = Rename(_corePath, CoreName + suffix, null, out byte[] corePdb);
                byte[] moduleImage = Rename(_modulePath, ModuleName + suffix, CoreName + suffix, out byte[] modulePdb);
                _candidateCore = Assembly.Load(coreImage, corePdb);
                _candidateModule = Assembly.Load(moduleImage, modulePdb);

                Type type = _candidateModule.GetTypes().FirstOrDefault(
                    t => typeof(IModModule).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface);
                if (type == null)
                {
                    _log.LogError("ModuleLoader: no IModModule implementor in " + _candidateModule.GetName().Name);
                    return false;
                }

                var module = (IModModule)Activator.CreateInstance(type);
                module.Load(_host);

                // Swap in only once the new module is fully live, so a failed reload (locked / corrupt /
                // half-written DLL) leaves the running module untouched rather than tearing it down.
                // The old generation is told a successor owns the game state now, so its Dispose drops
                // its hooks without handing the keyboard or the EventSystem back to the game.
                _host.SuccessorLoaded = true;
                try { DisposeCurrent(); }
                finally { _host.SuccessorLoaded = false; }
                _core = _candidateCore;
                _module = _candidateModule;
                CoreImage = coreImage;
                ModuleImage = moduleImage;
                Module = module;
                Generation = gen;
                _log.LogInfo("ModuleLoader: loaded " + type.FullName + " (generation " + Generation + ")");
                return true;
            }
            catch (ReflectionTypeLoadException ex)
            {
                _log.LogError("ModuleLoader.Load failed: " + ex);
                foreach (Exception inner in ex.LoaderExceptions ?? new Exception[0])
                    if (inner != null) _log.LogError("  loader: " + inner.Message);
                return false;
            }
            catch (Exception ex)
            {
                _log.LogError("ModuleLoader.Load failed: " + ex);
                return false;
            }
            finally
            {
                _candidateCore = null;
                _candidateModule = null;
            }
        }

        /// <summary>Tear down the current module (for shutdown).</summary>
        public void Unload() => DisposeCurrent();

        /// <summary>Rebuild from the current bytes, swapping the new module in only if it loads cleanly.</summary>
        public bool Reload() => Load();

        private void DisposeCurrent()
        {
            if (Module == null) return;
            try { Module.Dispose(); }
            catch (Exception ex) { _log.LogError("ModuleLoader: module Dispose threw: " + ex); }
            Module = null;
        }

        // A reference to a generation's renamed Core or Module (the module's own, a REPL script's)
        // binds here: the candidate while a load is in flight, else the live pair.
        private Assembly Resolve(object sender, ResolveEventArgs args)
        {
            string name = new AssemblyName(args.Name).Name;
            foreach (Assembly asm in new[] { _candidateCore, _candidateModule, _core, _module })
                if (asm != null && asm.GetName().Name == name) return asm;
            return null;
        }

        // The DLL's image with its assembly (and main module) renamed, and its reference to Core
        // renamed to match when one is given. The portable PDB beside it is rewritten alongside (a
        // PDB names the image it belongs to, so the original no longer fits the renamed one) and
        // handed back for Assembly.Load, so a reloaded generation's stack traces carry line numbers.
        // Without a PDB (or one Cecil cannot read) the image loads bare.
        private byte[] Rename(string dllPath, string newName, string coreName, out byte[] pdb)
        {
            pdb = null;
            byte[] image = File.ReadAllBytes(dllPath);
            string pdbPath = Path.ChangeExtension(dllPath, ".pdb");
            byte[] pdbIn = File.Exists(pdbPath) ? File.ReadAllBytes(pdbPath) : null;
            if (pdbIn != null)
            {
                try { return Rewrite(image, pdbIn, newName, coreName, out pdb); }
                catch (Exception e) { _log.LogWarning("ModuleLoader: " + Path.GetFileName(pdbPath) + " unusable, loading without line numbers: " + e.Message); }
            }
            return Rewrite(image, null, newName, coreName, out pdb);
        }

        private static byte[] Rewrite(byte[] image, byte[] pdbIn, string newName, string coreName, out byte[] pdbOut)
        {
            pdbOut = null;
            using (var input = new MemoryStream(image))
            using (var symbolsIn = pdbIn != null ? new MemoryStream(pdbIn) : null)
            {
                var read = new ReaderParameters();
                if (symbolsIn != null)
                {
                    read.ReadSymbols = true;
                    read.SymbolStream = symbolsIn;
                    read.SymbolReaderProvider = new PortablePdbReaderProvider();
                }
                using (AssemblyDefinition asm = AssemblyDefinition.ReadAssembly(input, read))
                {
                    asm.Name.Name = newName;
                    asm.MainModule.Name = newName + ".dll";
                    if (coreName != null)
                        foreach (AssemblyNameReference r in asm.MainModule.AssemblyReferences)
                            if (r.Name == CoreName) r.Name = coreName;
                    using (var output = new MemoryStream())
                    using (var symbolsOut = symbolsIn != null ? new MemoryStream() : null)
                    {
                        var write = new WriterParameters();
                        if (symbolsOut != null)
                        {
                            write.WriteSymbols = true;
                            write.SymbolStream = symbolsOut;
                            write.SymbolWriterProvider = new PortablePdbWriterProvider();
                        }
                        asm.Write(output, write);
                        if (symbolsOut != null) pdbOut = symbolsOut.ToArray();
                        return output.ToArray();
                    }
                }
            }
        }
    }
}
