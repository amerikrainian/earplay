# Earplay — Accessibility Mod for Word Play

Screen-reader accessibility mod for blind players of Word Play (Game Maker's Toolkit). Speaks UI
focus, menus, and (later) the round, the tiles, the shop and the modifiers via Prism. Ported from
guildrun-access (`C:\Users\CRNTJR3\cs\guildrun_access`): the host/module hot reload, the key-graph
navigator, the buffers, the key help, the dev server with its Roslyn REPL; reuse its patterns where
they fit. Speech is the sole interface: a silent failure is invisible to the player, so every catch
logs, and nothing caches game state.

## Game facts
- **Engine**: Unity **2022.3.50f1**, **Mono** (MonoBleedingEdge), x64. Game code is plain IL in
  `Word Play_Data\Managed\Assembly-CSharp.dll` (~490 KB, ~300 types, global namespace, unobfuscated);
  the assemblies are built against **.NET Standard 2.1**, so the host and the module target
  `netstandard2.1`. No interop proxies: game types are referenced directly, private fields are read
  through `Module/Interop/Private` (see Hard rules).
- **Install**: `C:\Program Files (x86)\Steam\steamapps\common\Word Play` (Steam app **3586660**). The
  build resolves it itself (`Directory.Build.props`: `-p:GameDir=`, a gitignored
  `Directory.Build.local.props`, `WORDPLAY_DIR`, the Steam registry, the default path).
- **Game code**: one `MonoBehaviour` per concern, most of them a singleton behind a static
  `Instance` set in `Awake` (`TitleScreenManager`, `ControlManagerScript`, `MetaGameRules`,
  `LogicManagerScript`, `ShopManager`...); upgrades and modifiers are one class each (`u*.cs`). Two
  scenes: `Title` and `Game` (`NavigationManager.titleScreenScene` / `gameScreenScene`), switched by
  EasyTransition's `TransitionManager`. UI is **uGUI + TextMeshPro**, animated by **DOTween**, many
  labels driven by **Febucci TextAnimator**, which leaves its markup in `TMP_Text.text`
  (`<noparse></noparse>`, `<wave>`, `<b>`): `TextFilter.Clean` strips it at speak time, and `/nav`
  shows it raw.
- **Input**: the **new Input System** through a `PlayerInput` (`ControlManagerScript.pi`) whose
  actions call the game's `InputAction.CallbackContext` handlers (`TitleControl.GoBack`,
  `KeyboardControl.*Key`), with **legacy Input also enabled**, so `UnityEngine.Input.GetKey(KeyCode)`
  works (verified). Typing a word in a round reads `Keyboard.current.onTextInput`
  (`KeyboardControl.OnEnable`), which a disabled keyboard device never raises: see Roadmap.
- **Modding framework**: **BepInEx 5.4.23.5** (Mono), vendored unpacked in `third_party/bepinex/`
  (`setup-bepinex.ps1` copies it into the game folder). HarmonyX 2.9, Mono.Cecil 0.10.4 in its core.
  No first-launch step: the build compiles against the game's `Managed` folder directly.
- **Speech**: Prism (`third_party/prism/prism.dll`, deployed next to `Word Play.exe`) via
  hand-written P/Invoke in the host (`src/Earplay/Speech/`). Prism talks to NVDA/JAWS/SAPI itself.
- **Language**: English only. No Unity Localization, no locale: the mod's strings are English with
  one override file (see Strings). If the game ever gains languages, port guildrun's `LanguageSync`.
- **Saves**: `%LocalAppData%Low\Game Maker's Toolkit\Word Play\` (`Application.persistentDataPath`):
  `fullgamemetasavedata.json` (`MetaGameSave`: progression, records, unlocks; `MetaGameRules`
  holds them live: `levelCompletion[]`, `highestScore[]`, `fewestWordsUsed[]`, indexed by mode id),
  `resumesavefile.json` (`SaveData`: the run in progress, `MetaGameRules.hasInProgressData`), and
  PlayerPrefs (the options: `axlowercase`, `axfont`, `MasterVolume`..., `consented`). The options
  page's Delete Save wipes all of it and reloads the title scene: never press Sure? for the player.
- **Mode ids** (`MetaGameRules.currentRule`, `LevelSelectScript.levelNames` index): 1 Easy, 2 Normal,
  3 Hard, 4 Legendary, 5 Marathon, 6 Quick Play, 7 Daily Challenge, 8 Ultramarathon (shown once 1-5
  are beaten); 0 is unused (`PlayGame(0)` resumes the saved run). 2-5 unlock when Easy is beaten.

## Decompiled reference
- `game/decompiled/` — ilspycmd's decompile of `Assembly-CSharp.dll`, one `.cs` per type, WHOLE
  method bodies (Mono IL decompiles cleanly). Gitignored: decompiled game code is never committed;
  regenerate with `uv run python tools/python/dump_game.py`. **Read the body before guessing a
  rule**: grep it (`Grep` over `game/decompiled`), it is the source of truth for what a method does.
- `game/analysis/version.json` — the Steam build id and `Assembly-CSharp.dll` stamp the decompile
  came from, so a stale reference is visible.
- Plugin assemblies (DOTween, SimpleScrollSnap, MyBox, TextAnimator) decompile the same way with
  `ilspycmd <dll>` when a screen needs one.
- For live behaviour use the game: `GET /gui` for structure, `POST /eval` for values (Dev driver).

## Tools (`tools/python/`, standard library only; run them with `uv run python tools/python/<tool>.py`)
- `dump_game.py [--keep] [--diff]` — after a game update: decompile again (ilspycmd, `dotnet tool
  install -g ilspycmd`), record the build id; `--keep` parks the old decompile as
  `game/decompiled.prev`, `--diff` lists the files the update added, removed or changed (the first
  pass at what to re-read). Then `dotnet build`: a renamed member fails the build.
- `dev.py <cmd>` — the dev server from the command line: `launch [--direct] [--mute]`, `kill`,
  `reload`, `nav`, `input ui.down ui.activate`, `speech --tail 20`, `log --grep X`, `gui --grep RE
  --context 3`, `eval file.cs`, `wait "<bool expr>"`, `typeinfo Name`, `actions`, `module`,
  `screenshot out.png`, `click [x y]` (an OS click at Unity screen coordinates). `launch --direct`
  runs `Word Play.exe` itself (Steam's launch drops the environment; `SteamAppId` keeps Steamworks
  from relaunching through Steam), `--mute` adds `EARPLAY_NO_SPEECH=1`.

Typical update loop: `dump_game.py --diff` -> re-read what it lists that the module touches ->
`dotnet build` -> `dev.py launch` -> drive the screens.

## Build & deploy
```
dotnet build            # or .\build.ps1
```
Debug build compiles all five projects and deploys host + Contracts + Roslyn deps + Core + Module
(DLLs and PDBs) to `<game>\BepInEx\plugins\Earplay\` and `prism.dll` next to `Word Play.exe`. **A
HOST or Contracts change needs the game closed** (their DLLs are locked) and a relaunch.
**Core/Module changes need NO restart**: `dotnet build src/Earplay.Module/Earplay.Module.csproj`
(or `.\build.ps1 -ModuleOnly`) copies just those, then `curl -X POST 127.0.0.1:8772/reload` or **F6**
in-game (`GET /module` shows the generation and the DLL write times, so a stale deploy is visible).

- First-time setup: `.\setup-bepinex.ps1`, then build.
- Launch: `& "C:\Program Files (x86)\Steam\steam.exe" -applaunch 3586660`, or `dev.py launch
  --direct --mute` for an unattended run. Kill from the Bash tool:
  `MSYS_NO_PATHCONV=1 taskkill.exe /F /IM "Word Play.exe"`. Wait for the dev server with
  `curl -s --retry 40 --retry-connrefused --retry-delay 2 --retry-all-errors http://127.0.0.1:8772/health`.
- Tests: `dotnet test src/Earplay.Tests/Earplay.Tests.csproj` (Core + Contracts; no game).
- Release: `dotnet build -c Release` compiles without deploying, and without the dev server (its
  sources are excluded and Roslyn is not referenced). Packaging, ported from guildrun (no manual yet:
  guildrun's `docs_src\` mdbook is the pattern when one is due):
  - `build_release.ps1`: the Release build, then `releases\Earplay-v<Version>.zip`, whose root is the
    game folder: the vendored BepInEx 5 (`winhttp.dll`, `doorstop_config.ini`, `.doorstop_version`,
    `BepInEx\core`), a `BepInEx.cfg` with the console off, `prism.dll`, and
    `BepInEx\plugins\Earplay` (the four mod DLLs, `lang\*.txt`). Version from `Directory.Build.props`.
  - `build-installer.ps1`: `installer\` (Rust + wxWidgets, from the Non-Visual Calculus installer;
    needs cargo, libclang and ninja, probed from Visual Studio) to `releases\EarplayInstaller.exe`.
    It finds the game (`WORDPLAY_DIR`, Steam libraries; markers `Word Play.exe` and
    `Word Play_Data\Managed\Assembly-CSharp.dll`), downloads the newest `Earplay-vX.Y.Z.zip` from
    `amerikrainian/earplay`'s releases, records what it wrote in
    `BepInEx\config\Earplay\install.json`, backs up what it overwrote, and uninstalls from that.
    `--cli` gives a console version; `cargo run --release --example cli` runs it without the UAC
    prompt, and `EARPLAY_INSTALLER_RELEASES_URL` points it at a local feed for an end-to-end test.
  - `test-installer.ps1`: the installer's `cargo test`.
  - `create-release.ps1 vX.Y.Z`: for a pushed tag, `gh release create` with the zip and the
    installer, the notes from the tag's `## VX.Y.Z` section of `changelog.md`.

## Logs
Our lines go through the BepInEx logger with an `[Earplay]` source into
`<game>\BepInEx\LogOutput.log` (truncated each launch), and in-band via `GET /log?since=N&grep=S`.
The game's own `Debug.Log` lines do NOT reach that file (or `/log`) in this setup: they are in Unity's
`%LocalAppData%Low\Game Maker's Toolkit\Word Play\Player.log` (the previous run's in
`Player-prev.log`), which is how a game-side rule shows ("Haven't played today's daily challenge",
"Changing control type to Keyboard"). BepInEx boot problems are at the top of `LogOutput.log`.

## Navigation strategy (decided)
**Custom keyboard navigation over the live uGUI widgets** — NOT the game's own EventSystem selection.
We build a key graph per screen from the live views (`Build(GraphBuilder)`, immediate mode: declared
fresh every render, focus persisting by `ControlId` identity), read labels from TMP text at speak time,
and activate through the widget's own event (`button.onClick.Invoke()`), so the game's handler runs as
for a click. While focus mode owns the keyboard, `FocusMode` mutes uGUI navigation events
(`EventSystem.sendNavigationEvents = false`) and clears the selection. Screens resolve their activity
from the game's own state, never by scanning: a singleton's `Instance` (Unity's null check: a
destroyed singleton from a previous scene compares equal to null) and the fields it holds.
`FindObjectOfType` / `Resources.FindObjectsOfTypeAll` are for `/eval` and the dev audit only.

**Tab-stops**: Tab and Shift+Tab always wrap round a screen's stops; on a screen with one stop they
do nothing at all (consumed, silent). A context names its group by its label alone: there is no
"list" role word.

## Architecture — HOST/MODULE split (hot reload; the guildrun pattern on Mono)
Five projects:
- **`Earplay.Contracts`** (netstandard2.0, PERMANENT): `IModHost`, `IModModule`, `IDevDriver`,
  `ISpeechBackend`, `SpeechPipeline`, `ISettingsStore`/`ModSettings`, `TextFilter`. Loaded once, by
  name; everything the host holds is typed here.
- **`Earplay`** (netstandard2.1, PERMANENT host; **changing it needs a game restart — keep it
  minimal**): `Plugin` (the `BaseUnityPlugin` entry), `Speech/` (Prism), `Host/HostPump` (the one
  MonoBehaviour, on its own `DontDestroyOnLoad` GameObject), `Modularity/ModuleLoader`,
  `BepInExSettingsStore`, `Dev/` (dev server). The host must NEVER reference Core at compile time.
- **`Earplay.Core`** (netstandard2.0, RELOADABLE, engine-free, unit-tested): `Graph/` (the key-graph
  engine: `KeyGraph`, `GraphBuilder`, `GraphAnnouncer`, `ControlId`), `UI/` (`GraphNavigator`,
  `GraphSheet`, `ControlTypes`, `TypeAheadSearch`, `KeyHelp`, `EditEcho`), `Input/` (`InputManager`
  categories + chord shadowing), `Screens/` (`Screen`, `CompositeScreen` + `ScreenSection`,
  `ScreenManager`, `ChoiceSubmenuScreen`), `Buffers/`, `Strings/` (the authored strings table).
  Engine seams are statics the module sets in Load: `CoreLog`, `Speech.Speak`, `NavInput.Current`,
  `Navigation.FocusActive`, `InputManager.FocusActive`, `GraphAnnouncer.PositionText/ExpandedStateText`.
- **`Earplay.Module`** (netstandard2.1, RELOADABLE, engine-coupled; **day-to-day feature work goes
  here**): `ModuleMain` (IModModule + IDevDriver: seams, input registration, screen registration,
  focus mode, Tick), `Screens/Title/` (the title scene's pages, below), `Screens/` (the key help, the
  mod menu), `UI/GameNodes` (node factories over uGUI Button/Toggle/Slider/TMP_Dropdown/TMP),
  `UI/FocusMode`, `UI/Buffers`, `UI/TextEdit`, `Input/KeyboardBinding` + `UnityNavInput`,
  `Interop/Private`, `Dev/ScreenAudit`. A long screen decomposes into sections when they carry state
  or act on each other; a screen that only reads one panel stays whole (never partial classes).
- **`Earplay.Tests`** (net8.0 xUnit): Core + Contracts only; runs serially (static seams).

**The loader on Mono** (`Modularity/ModuleLoader`): Mono has no collectible load contexts, so every
generation is a fresh pair of assemblies in the one AppDomain and the old pair stays loaded (a few
hundred KB a reload). Mono binds a reference by NAME to an assembly of that name it already has, and
BepInEx 5's own `AssemblyResolve` handler (registered before ours) searches the plugins folder
recursively for `<name>.dll` and would load Core from disk by path. So before loading, Mono.Cecil
renames each generation's pair (`Earplay.Core.g3`, `Earplay.Module.g3`, the module's reference to Core
rewritten to match) and rewrites the portable PDB beside each so stack traces keep their line numbers;
the names are unique, so a reference can only bind to this generation's copy, which the loader's own
resolve hook hands back. Namespaces and type names are untouched. Verified: after a reload
`ModuleMain`'s assembly is `Earplay.Module.g2` referencing `Earplay.Core.g2`, the g1 pair still loaded
beside it. The REPL compiles against the live generation's images (`ModuleLoader.CoreImage /
ModuleImage`), so `/eval` reaches `Earplay.Module.ModuleMain.Host` and every Core static.

Rules: new feature/screen/adapter/patch code goes in Module (Core when engine-free). Only entry, native
handles, sockets, and component types go in the host (a component type defined in a byte-loaded
module would be a new type every generation). Module Harmony uses a per-load GUID id and
`UnpatchSelf()` in Dispose. The host loads the NEW generation BEFORE disposing the old one
(failed-reload safety) and sets `IModHost.SuccessorLoaded` around the old Dispose: a disposing module
restores suppressed game state (the keyboard device, EventSystem navigation) only when that flag is
false (a shutdown), and just drops its hooks on a reload. Core statics are per-generation.

## Dev driver (Debug only; on by default, `EARPLAY_NO_DEV=1` disables, `EARPLAY_DEV_PORT` sets the port)
Loopback HTTP on `http://127.0.0.1:8772` (guildrun's is 8771, so both can run), driven with curl.
`EARPLAY_NO_SPEECH=1` skips Prism for headless runs; spoken text is still captured. The game keeps
running when unfocused.
- `POST /input` body = `up|down|left|right|confirm|back|tab|prev|home|end|secondary|tooltip|read` or any
  registered action key (`ui.down`, `mod.focus`, `mod.help`): routed through the module's real
  dispatch, exactly like a key press; the response echoes the new focus readout. `GET /actions` lists
  the keys. Before a `confirm`, read the readout the previous input returned: a guessed focus has
  toggled full screen on the main menu, and the options page's Sure? deletes every save.
- `GET /nav` our navigator's state (focus mode, screen stack, focused node, every node in the render).
- `GET /gui` raw uGUI hierarchy (paths, components, TMP text, CanvasGroup alpha) to reverse-engineer a
  screen; `GET /focus` the game's own uGUI selection. Diff `/gui` against `/nav` to find what we miss.
  The title scene keeps all five pages active at once: `/gui` lists them all, alpha tells them apart.
- `GET /speech?since=N` what the mod spoke, tagged interrupt/queue and speaking class (`&wait=MS`
  long-polls); `GET /log?since=N&grep=S`.
- `POST /eval` C# against the live game (Roslyn scripting on Mono; state persists; references every
  loaded assembly, every DLL in the game's `Managed` folder, and the live Core and Module from their
  images); `POST /wait?timeout=MS` a per-frame bool expression; `GET /typeinfo?name=X` (declared
  members, private ones included); `GET /screenshot`. Private fields in `/eval`: reflection,
  `typeof(T).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(obj)`. A button's
  wiring: `button.onClick.GetPersistentTarget(i)` / `GetPersistentMethodName(i)` (most of the title's
  buttons are wired in the scene, not in code).
- Roslyn 4.8's netstandard2.0 build needs `System.Memory`, `System.Buffers`,
  `System.Numerics.Vectors` and `System.Threading.Tasks.Extensions` as ASSEMBLIES, which a
  netstandard2.1 restore treats as inbox and never copies (the game's mscorlib has the types, not the
  assemblies): the host references them `ExcludeAssets="compile"`, runtime copies only. Without them
  the first eval throws a `FileNotFoundException` for System.Memory 4.0.1.2.
- `POST /reload`, `GET /module` (generation, DLL write times, Harmony patch table), `GET /health`.
- `POST /input` with body `dev.audit` (`Module/Dev/ScreenAudit`) is the coverage report of the screen
  on show: every node of the render with its buffer lines, then the visible texts and interactable
  widgets none of that covers. Substring matching over tag-stripped text, so it over-reports: a listed
  text is a lead, not a verdict (a widget with no caption, such as an icon button, always lists). It
  scans the scene, so it is for the dev driver only. The main menu's expected leftovers: the logo's
  letter tiles, the tip's two arrow buttons (Left/Right on the tip), the two icon buttons the mod names.
- `POST /input` with `dev.type:TEXT` (`UnityNavInput.InjectTyped`): TEXT reads as typed on the next
  frame by type-ahead.
- The launch update check (`Module/UpdateChecker`, `Core/UpdateCheck`) asks `amerikrainian/earplay`'s
  latest GitHub release and logs "update check: failed (404 (Not Found))" until the repository has
  one; that warning is expected.

## Keys (focus mode on at launch; Ctrl+Shift+A toggles it)
F1 is the key help (below). Arrows navigate, Tab/Shift+Tab cycle control groups (always wrapping),
Enter activates, Backspace is the secondary action, Escape backs out (a page's Back button on the
title scene), Home/End jump to the ends (of a horizontal row when focus is in one: the board's first
and last column, `GraphNode.InRow`; of the stop otherwise), Alt+Up/Down jump sections, Left/Right
adjust a slider or a stepper (the tip, the font), typing letters searches the focused group (on the
round, letters are the game's typing instead). Delete is the focused thing's removal (`ui.delete`). Ctrl+arrows review the buffers (below).
Ctrl+Shift+M opens the mod menu (settings, all keys). F6 reloads the module (Debug).

**Key help (F1, `Module/Screens/HelpScreen`, `Core/UI/KeyHelp`)**: the keys that would do something
RIGHT NOW, for the focused screen and control. An action is listed when a key of it is live
(`InputManager.IsLive`: category active, chord unshadowed) and something answers it: the navigator
asked without acting (`Navigator.WouldHandle`: an arrow with a transition that way, Enter on a node
with `OnActivate`, Tab only with two stops or more, any key the focused screen offers through
`GetActions`), else its own handler when `InputAction.IsAvailable`. So a handler key that is silent
somewhere must say so with `.When(...)`. A key the screen offers reads by the SCREEN's label. Enter on
a row runs the key as a press would. The mod menu's "All keys" is the unconditional list.

## Buffers (the Harkest Dungeon pattern: `Core/Buffers`, `Module/UI/Buffers`)
Review lists for the information a focus announcement leaves out, read live on every keypress:
Ctrl+Right/Left switch buffers (speaking "name: current line"), Ctrl+Up/Down step lines. Empty buffers
are skipped; a focus change re-homes review to the control's buffer. So far one buffer, **control**:
the focused node's head line (label, value, state, never the role word or the position), then one line
per description part and per `NodeVtable.Details` line (`Core/Buffers/NodeLines`). The round's lists
(the word log, the letter bag, the owned modifiers) join it as their screens land. A detail line is
ONE fact; helpers return `List<string>`; a node's details go in `Details = () => ...`, never spoken
directly.

## Strings (`Core/Strings/Strings.cs`, `lang/`)
Every mod-authored word is a key in `Strings.Defaults` with its English value; typed accessors read it.
Word Play is English only, so there is no language following: `ModuleMain.LoadStrings` reads
`lang/strings.txt` beside the plugin when it exists (key=value, the `{n}` slots kept), at load and on
every reload, and logs what it set aside (`TranslationReport`). `lang/en.txt` is
`Strings.DumpTemplate()`, the starting point for that file: after adding or rewording a string, run
the tests once with `EARPLAY_WRITE_LANG_TEMPLATE=1` (`LangTemplateTests` fails while it lags). Key
names in the key help ("Ctrl+Shift+A", "Up Arrow") are not translated.

## Settings (`Contracts/ModSettings`, the host's BepInEx config)
`<game>\BepInEx\config\com.amerikranian.earplay.cfg`, owned by the host so values survive reloads,
edited in the mod menu's Settings: `focus_mode_on_launch` (default on), `speak_positions` (list
positions, "2 of 5": default OFF), and `speak_coordinates` (a board cell's "column row" after its
contents, and in the new-tiles line: default on), `score_breakdown` (a word's scoring step by step:
default on). Settings only the module reads live in
`Module/ModuleSettings`, keyed straight into the host's store, so adding one needs no host change. A new default reaches only a fresh config: BepInEx writes the
bound value the first time and keeps it.

## The title scene (`Module/Screens/Title/`)
Five pages under one canvas, all active at once; `TitleScreenManager.OpenPage` cross-fades their
CanvasGroups and first sets `TitleControl.currentControlZone` (the manager's private `controlScript`)
to the incoming page. That zone is the one fact of which page is the player's: each page screen is
active exactly while it names it (`TitlePages.Showing`). Each page but the logo page has a direct
child "Back Button" wired to `ReturnToHome`, which the game's Escape (`TitleControl.GoBack`, a
PlayerInput callback, deaf while focus mode disables the keyboard) presses; our screens offer Escape
as the same press (`TitlePages.Back`).
- **Main menu** (`MainMenuScreen`, the logo page `logoGO`): one stop, "Main menu": the "Buttons"
  column in its order (Play, Resume Run, Extras, Quit; Resume Run greyed out without a saved run, and
  with one the resume bubble's "Classic, Round 3", `resumeText`, read as its description), the Daily
  Challenge tile while the game offers it (its `DCcanvasGroup` interactable: Easy beaten and
  `dailyChallengeExists`, off in this build), read as its label, the date ("NOV 12") and its state
  tag ("Not Played", "Resume", "4 Words", "Game Over!"); then the corner's caption-less icon buttons,
  Options and Full screen (a toggle, its state `Screen.fullScreen` read live: the switch lands a frame
  or more after the press), then the tip (`TipSystem.tipArea`; Left/Right are its arrow buttons,
  `PreviousTip`/`NextTip`, the new tip the feedback) and the version line, so Tab does nothing here. The logo's letter
  tiles are decoration. Play opens the level select directly in this build (`OpenLevelSelect`; the
  consent page's `OpenConsentScreen` is wired to nothing).
- **Level select** (`LevelSelectScreen`, `levelSelectGO` + `LevelSelectScript`): the "Picker"
  groups, each a context named by its label ("STRATEGIC", "CASUAL"). The two panels sit side by side on
  ONE line (Casual right of Marathon, checked by their positions), so all the tiles are one row:
  Left/Right walk it, crossing into Casual names the group; Up/Down go on to Play and Back, and back up
  lands on the selected tile. Each tile is a radio button named by `levelNames[i]`, `i` its index in
  `pickerTiles` (index 0 is an unused duplicate of Easy), the index its click hands to `ChangeDetails`.
  Landing on a tile clicks it (`OnFocus`), a locked one too (the game selects it and greys Play out),
  so the details panel follows focus and the tile reads "selected", then the description and the
  record ("Not Yet Played", "Complete Easy Mode to Unlock", "Finished in 12 Words"); "locked" from
  `lockIcons[i]`, "completed" from `goldenOverlays[i]`. Then Play (`playButton`, disabled on a locked
  mode) and Back. Check a page's layout by its RectTransform positions in `/eval`, not by the
  hierarchy: two panels are not two rows.
- **Options** (`OptionsScreen`): three stops named by their panel titles, read in visual order (the
  hierarchy lists them right to left), then Back. Options: Toggle Full Screen (with the state), the
  SFX and Music sliders (0 to 1, read as percent, a tenth a step; `AudioSliderController` applies
  `onValueChanged`), the resolution dropdown (named by the mod). Accessibility: the aids are Buttons
  whose `OptionTutorial.ticked` is TRUE when the aid is OFF (`Start` crosses the box when the pref is
  0), read as toggles; the font switcher (`DyslexicToggle.currentInt`, 0 the game's default, read
  off; 1 to 3 index `AXManager.fontsToUse`, read by `faceInfo.familyName`: Atkinson Hyperlegible
  Next, OpenDyslexic, Sofia Pro Soft), Enter/Right `NextFont`, Left `PreviousController`; the example
  tiles are a preview and are left out. More...: Report an Issue (opens the studio's page in the
  browser) and Delete Save, whose press slides out Sure? (`MoveableButton`, listed while
  `sureShowing`). Sure? runs `DeleteAllSaveData`.
- **Extras** (`ExtrasScreen`): Stats ("Games Won: 0", one line per row, an empty value read blank),
  Credits line by line, Back.
- **Consent** (`ConsentScreen`): the analytics message line by line, No Thanks, Okay!. Never pressed
  for the player.

## The game scene (`Module/Screens/Game/`)
- **Tutorial panel** (`TutorialScreen`, `TutorialManager.tutorialGO`, "Tutorial Screen"): the "How to
  Play" card before the first round's tiles are dealt, and the same panel for every later lesson
  (`AttachTutorial`). A modal (layer 30, exclusive): the title (`labelTA`'s TMP text) as the dialog
  context, then each active part's line (`tutorialParts[i]` / `textAreas[i]`; the picture tile beside
  each line only illustrates it), then "Got it!", which runs `ShutTutorialScreen` (the first one then
  deals the tiles, `GridManagerScript.ReturnFromTutorial`). Escape presses it too.

- **The round** (`Module/Round/`, `RoundScreen`, a `CompositeScreen` of two stops, the board (with
  the word strip as its last row) and the status; active while
  `Round.OnScreen`: the game's `GamepadControl.currentControlZone` is LetterGrid, or Menu / LetterBag /
  Tutorial, the panels that open over the grid and return to it, so focus survives them;
  `KeepStateOnPop` keeps it across the shop; coming on screen it waits for
  `ControlManagerScript.canInteract` once, so the landing reads the new round's settled board, not
  a tile the round's deal or refresh is about to replace). It LENDS THE KEYBOARD (`FocusMode`, `IKeyboardLender`):
  the device stays enabled and the game's clashing actions are disabled by name (Navigate, Interact,
  Nav Left/Right, Close Gaps, Submit Tiles, Backspace, Options), so letters type into the game
  anywhere on the round (`KeyboardControl.OnTextInput`, the game choosing the tile: mandatory, then
  special, then a copy not just returned), and Shift (ing/ers), 1 and 4 (drawers), 2 Shuffle (hold:
  sort), 3 Refresh (held), [ ], 8 (wildcard), = (plus) are the game's own. No type-ahead there.
  - **Board** (`Round.Board`): the tile grid as drawn (`GridManagerScript.tileHolders`' parent, a
    GridLayoutGroup of `constraintCount` columns: bonus slots and Shuffle in column 1, letters in
    2 to 5) plus the tool column ("Grid of Tools": Backspace, Refresh, Bag, Submit, one per row) as
    column 6; rows share a row key (Up/Down keep the column). A cell is its contents, then "column
    row", 1-based, row 1 at the top: "C, 3 points, 4 1", "A, 1 point, emerald, 3 3", "E, 1 point, in
    word, 2 4" (a tile in the word leaves a faint copy in its spot), "locked slot, 1 1",
    "Refresh, 5 left, 6 2", "Letter Bag, 80 tiles, 6 3", "Submit, disabled, 6 4". Tools are told apart
    by wiring (their scripts share one "Tools" object): Submit is `submissionButton`, Refresh has a
    `RefreshHelper`, Backspace / Shuffle are their `LongHoldScript.onShortPress` targets, Bag's
    onClick is `ToggleLetterBagDrawer`. Tool names are the game's (`BonusDescriptions.ShopDescription`
    titles: Refresh, Shuffle or Sort by `ShuffleTool.defaultIsShuffle`, Letter Bag) or the mod's
    (Backspace, Submit). Special tiles' and tools' descriptions (the same `ShopDescription`) are the
    control buffer. Enter on a tile is `TileHolderScript.ControllerInteractTile` (the game's keyboard
    press: to the word, or back from it, or onto an upgrade in click-to-upgrade mode); on a tool, the
    tool's own method (`BackspaceButtonClick`, `PressedRefreshButton` without the hold,
    `ShuffleButtonOnPress`, `PressedSubmitButton`, the bag as `BagScreen`). The first visit lands on
    the first letter tile.
  - **Word** (`Round.WordSlots`): the shown submission slots (10, 20 once the tenth fills) as the
    BOARD STOP'S LAST ROW, sharing the board's row key, so slot n sits under column n and Up from slot 7
    and beyond reaches column 6 (shared-key rows clamp to a shorter row's last item,
    `GraphBuilder.VerticalTarget`); entering it says "Word"; Alt+W jumps to it keeping the column (board column n to slot n; from the
    status, slot 1), as a move (`Navigation.MoveTo`). Each slot:
    "C, 3 points", "empty, +5" (the slot's bonus as drawn), "empty, required" for the first four
    (`numberOfTilesRequired`), and a slot's multiplier badge after either ("3x": an upgrade's,
    `SubmissionTileSpace.hasMultiplier` / `multiplierTex`). The game has NO score preview: modifiers
    apply only at scoring; before it, a tile shows its own value alone (a mirror or "!" tile's value
    updates live, `SubmissionQueueChanged`, and `GetTileScore` reads it). No slot numbers: a typed or pressed tile goes to the FIRST empty slot
    (`PutTileInFirstEmptySpace`), so the word fills in order; only a mouse drag places a tile
    anywhere. Enter submits from any slot (`PressedSubmitButton`:
    its own refusal, "Words Must Have At Least 4 Tiles", comes back through the strip), Delete sends
    the slot's tile back.
  - **Status**: the score bar's "0 / 35, 35 Points Left", "ROUND 1 / 10, EASY MODE" (BarScript's
    `barLabel`, `scoreToGoTMP`, `roundText`, `leftText`, through `LogicManagerScript.scoreBarScript`),
    "PLAYS: 10" (`lifeBarScript`), refreshes, the bag count, the strip's text while it shows.
  - **Moving a tile** (`Round/TileSwap`, Space, `ui.drag`): the first press on a board tile or word
    slot picks its tile up (the mod notes it only: the tile stays put, its readout says "held", typing
    still works); a press on another board tile or slot drops it through the game's own drop, the
    gamepad drag's calls (`TileDrag.OnBeginControlDrag`, then `DropTile(target holder)`), so the
    mouse drag's rules hold: board tiles swap, a slot takes it (an occupied one swaps), a word tile
    dropped on the board goes back to its spot, locked and stuck tiles refuse. Every drop path ends in
    `FinishDrop`, which clears `holdingATile` (typing is refused while it is set). Space on the held
    tile, or Escape, puts it down. Result 25 frames on, the word row's own lines quiet meanwhile: the
    target re-read as landing on it reads it ("N, 1 point, 3 1"; "N, 1 point" in the word), or "can't
    move N there" (the tile's parent unchanged: the game sent it back). Verified live: a board swap and back, a drop into an empty slot, a cancel.
  - **Keys**: Backspace (every round node's secondary) the last tile out (`BackspaceButtonClick`, the
    game's LIFO list), Shift+Backspace the whole word (`BackspaceButtonHold`), Ctrl+Backspace close
    gaps (`CloseAllGaps`), Ctrl+H jumps to the special round's highlighted tile
    (`GridManagerScript.mandatoryTile` while `mandatory`: its word slot when in the word, else its board
    spot; offered, and so in F1, only while one is highlighted), Shift+Enter submits from anywhere on
    the round (`PressedSubmitButton`, as
    Enter on the word or the Submit tool does), Escape the Options drawer (`TopDrawerManager.ToggleOptionsDrawer`). The
    game's tool keys are ours on the round (its Shuffle Tiles, Refresh Tiles, Type Wildcard and Insert
    Plus actions blocked), so F1 lists and runs them: 2 the shuffle tool's press (labelled Shuffle or
    Sort by `defaultIsShuffle`), Shift+2 the other (the game's hold on 2), 3 Refresh held a second
    (timed by `RoundScreen.OnUpdate` off the action's `Held`; a short tap calls the game's own
    `TutorialMessage("Press and Hold to Refresh")`; dispatched with no key down, from the help, it
    refreshes at once), 8 and = the wildcard and plus tiles (offered only while the board has one).
    The drawer keys (1, 4, [ ]) and Shift+I/E stay the game's until the drawers are screens.
  - **Events** (`RoundEvents`): the word row diffed every frame while `Round.Playing` and interaction
    is on ("C" interrupting, "C removed"); Harmony on `KeyboardControl.Update` ("no Z" when a
    typed letter found no tile), `SubmitWord.SubmitTheWord` (board noted), `WordWasNotAccepted`
    ("SMOE: not a word"), `ReturnToGame` / `LogicManagerScript.FinishRound` ("BREAD: 13", the score
    line, "round complete", "new: E, 3 1, T, 4 2, ..." from the board diff, 15 frames on),
    `LastWordArea.TutorialMessage` and `ShowSpecialRoundText` (the strip; the rule once a round, by
    round and text: the game redraws it after every word), `ShuffleTool.ShuffleTiles` /
    `SortTilesAlphabetically` ("shuffled" when tiles moved), `RefreshTool.RefreshAllTiles`
    ("refreshed, 4 left"). Verified live: BREAD typed, submitted, scored 13, five new tiles named.
  - The driver cannot type into the game: call the private `KeyboardControl.OnTextInput((char)'b')`
    through `/eval` (the game's own text-input handler), one letter at a time.
- **Modifiers and upgrades** (`Round/Drawers`, stops of the round between the board and the status,
  present once one is owned; the drawers need not be open, their buttons answer either way):
  - Modifiers: `BonusInventory.allModifierStubs` with `hasAPurchasedModifer`, in sibling order (the
    drawer's, which is the scoring order): the stub text (`myDescription`, "Refresh on New Round"), the
    score badge when `wordScoreArea` shows, "disabled" by `disabledOverlay`; the control buffer is the
    card's `shopDescription` then its `additionalDescription` text. Enter is `ToggleCard` (slides out
    Sell, and focus lands on it once the slide's tween sets `sellIconIsShowing`, polled while
    `stubIsMoving`); while `sellIconIsShowing` a "Sell, +1" node (`sellAmount`'s two lines) follows the card,
    Enter on it `SellStub`, then the refreshes line. Space reorders (`Round/ModifierMove`): Space picks
    one up (its readout says "held"), Space on another moves it into that place, the end of the
    game's drag (`SetSiblingIndex` to the target's under `modifierParent`, then `RefreshStubList`,
    which rebuilds `ownedModifiers` in that order); focus follows it and the landing re-reads it. Space on the held one, or Escape, puts it down. Node ids are the stub's name
    (`RefreshStubList` renumbers `allModifierStubs`).
  - Upgrades: `allUpgradeStubs` with `hasAPurchasedUpgrade`: stub text, "3 of 4 uses left"
    (`UpgradeGetNumberLeft` of `UpgradeMaximum` + `extraUpgradePoints`), "picking a tile" while it
    waits. Enter: `PlayerClickedUpgrade` (the game's "Pick a tile to upgrade", then Enter on a tile
    is the game's own press in that mode, `ControllerInteractTile` -> `SendToUpgradeCard`); a
    `HasButton` upgrade calls `TriggerBonusButtonScript` directly (the game's press reaches it only
    in keyboard/controller control type, which focus mode never sets). Escape on the round cancels
    the pick (`CancelClickToUpgradeMode`). Space with a tile picked up (`TileSwap`) on an upgrade
    drops it there: `DropTile(stub.dragSlotScript.gameObject)`, an upgrade drop target, the game's
    drag-and-drop route ("can't move N there" when no `UpgradeTile` follows). Every route ends in
    `UpgradeStubScript.UpgradeTile`, which `Round/UpgradeConfirm` hooks: it notes the tile and its
    board spot (`myHolder`), waits for interaction to resume (the coroutine resumes it after putting
    the tile back), and re-reads the tile as landing on it reads it ("A, 1 point, diamond, 3 1"). The Swap Tile picker is `Round/PickerScreen`.
- **Swap Tile picker** (`Round/PickerScreen`, zone TilePicker while `PickTileManager.pickerCG` is
  interactable; opened by a Pick a Vowel / Pick a Consonant upgrade, `UpgradeTile(openTilePicker)`):
  a modal under its title ("Swap Tile"): "replacing E, 1 point" (`tileIWillChange`), the letters as
  one row (`tileButtons`, `tileTMPs`, `scoreTMPs`: "B, 3 points", disabled when the bag has none),
  Enter picks (the button's `PickTile`), then Cancel; Escape is `PickTileManager.Cancel` (the tile
  back unchanged). The pick reaches `UpgradeStubScript.ChangeTile`, which `UpgradeConfirm` also
  hooks, so the swapped tile is re-read.
- **Shop** (`Screens/Game/ShopScreen`, while `ShopManager.shopIsShowing` and the zone is Shop): its
  title ("Round 1 Complete", `shopTitleText`) names the screen; ONE stop: the plays the round
  brought ("+4 Plays", `livesOnNewRoundPop`), the cards (`upgradeCards`) as one row: kind, rarity,
  description (`headerText`, `rarityText`, `myDescription`), an upgrade's uses
  (`associatedBonus.UpgradeMaximum`, the dots), "All Modifier Slots Filled" when shown
  (`allFilledGO`); the card's `additionalDescription` text is the control buffer; Enter buys
  (`myButton` -> `BuyThisCard`). Then Re-Roll and Skip as drawn, caption then cost ("Re-Roll, -1
  Plays": the "Left Text" first; `reRollButtonText` is the COST text), the letter bag (`BagScreen`),
  Peek, the plays line. A re-roll reads the new cards once each shown card holds the one dealt to it
  (`BonusStore.nextThreeUpgrades[i]`: `SpinCards` fills them one at a time, half a second apart). Each shop opens on the first
  card (`InitialFocusNode`); focus is kept only across a peek (`KeepStateOnPop` reads `peeking`), so
  a new shop never opens on the place of the card bought last time. Not a keyboard lender: the game's
  keys are off in the shop.
- **Peek** (`Round/PeekScreen`, while the shop is showing and `ShopManager.peeking`): the shop's
  Peek (`TogglePeek`, a fade of the shop's canvas; the zone stays Shop) shows the board, read by the
  round's own sections in read-only form (`BoardSection(readOnly)` and kin: every cell reads, Enter,
  Space, Backspace and Delete do nothing, and the keyboard is not lent). Escape toggles the peek off,
  back to the shop.
- **Score breakdown** (`Round/ScoreBreakdown`, on by default, mod menu "Score breakdown" /
  `score_breakdown`; off = the word and total only): a word's scoring read as the player watches it,
  each step queued as the game shows it, from its display calls: a tile's points on its slot
  (`SubmissionTileSpace.showScore`, "B, 3"), `SpecialTileManager.PingEmerald` ("emerald hit"),
  `PlayGoldSubmissionSound` ("golden 2"), `PingDot`'s result ("dot, times 3"),
  `ScoreZoneScript.FinishGold`, a modifier's flash (`ModifierStubScript.PingModifierStub`: its stub
  text; also outside a word), the total (`BarScript.ChangeFinalScore`, parts from `SubmitWord`'s
  `wordScoreForLog` / `bonusScoreForLog`: "12 plus 10: 22"), a modifier's change to it
  (`ChangeFinalFinalScore`), the plays popup (`LifeBarScript.PopupText`, while `Round.Playing`).
  Flashes of the moment the word is accepted (`OnCorrectWordSubmitted`, `LivesToLose`: from
  `SubmitTheWord` to `BarScript.FadeInScore`) are held: dropped when the same modifier flashes again
  while the word scores, said before the total otherwise (a modifier that acts only then).
  With it on, the word's closing line names the word alone ("PADDING"), the total already given;
  off, "PADDING: 97". Verified live on PADDING (golden tiles, two modifiers).
  The game has no score preview before submitting.
- **End screen** (`Screens/Game/EndScreen`, zone EndScreen while `LogicManagerScript.gOScreen`'s
  `gameOverScreen` is shown): its title (`topText`: "You Win!", "You Lose...", "Not Enough Tiles!")
  names it; one stop: every shown text of the panel but button captions, line by line (the stats:
  "Words Spelled: 39", "Perks Purchased: 9", "Best Word: PADDING - 97", reached round, a new best),
  then the "Panel/Buttons" row's children through `UI/OptionRows` (Endless Mode or Play Again, Quit to
  Menu with its Sure?). Escape does nothing.
- **Quick Play** (rule 6, `RuleInstance.simpleMode`, `LogicManagerScript.inSpellingMode`): the
  round's own screens. Rounds are words (`wordsSpelled` of `wordsNeededForBonus`), the goal medals.
  Status adds "Next Round in 4 Words" (`NextPerkInfo.numberText` while its `myCG` shows; also spoken
  after each word) and "medal: silver" (the score bar's `medal` icon and its `medalLetter` B/S/G);
  the round line already carries "NEXT MEDAL: 500" / "HIGH SCORE: ..." (`roundText`); `BarScript.
  WonAMedal` is hooked: "silver medal won". The mode's card (`SpellingModeCard`) shows the run's
  modifier, which is also modifier stub 0, read by the Modifiers stop. Its shop has no re-roll or
  skip, its title is its own label ("Next Round...", under "Elements/Labels", not `shopTitleText`),
  and its cards repeat the kind in the rarity slot ("Modifier, Modifier", read once). Verified live:
  four words to the shop.
- **Special-round slot marks**: a slot a "letter must be a vowel" round marks
  (`TileHolderScript.mustBeVowel` shown, `SubmissionScript.MakeALetterMustBeVowel`) says "must be a
  vowel" after its tile or empty line.
- **Glass tiles** (the duplicate-to-glass upgrade: `UpgradeStubScript.glassTile`, parked in the
  stub's own slot, a `TileHolderScript` with `upgradeSlot`): the upgrade reads "glass E, 1 point,
  ready" (or "in word"); Enter on it is the game's press on that slot (`ControllerInteractTile`:
  the glass tile into the word, or back). A tile reads "glass" for `myType` Transparent or the
  `transparent` flag.
- **Bag** (`Round/BagScreen`, Enter on the Bag tool): `LetterBagManager.LetterBag` and `SpentLetters`
  (tile id strings: letter, type code S G D P F E T L, two digits of bonus points, two of special
  score) counted by how they read, "A, 1 point: 6"; a modal, Escape closes. The game's own drawer is
  not opened.
- **Options drawer** (`Screens/Game/OptionsDrawerScreen`, open while `TopDrawerManager.optionsDrawer`
  `drawerIsOut`): the drawer panel's rows through `UI/OptionRows` (shared with the title's options
  page): Give Up and Quit to Menu (`MoveableButton`s, Sure? listed while out), Report an Issue,
  Toggle Display Scale, the full-screen switch, Controller Icons, Music, SFX, Show Tooltips, the
  shuffle default. Escape closes it with the same toggle. Never press a Sure? for the player.

## Hard rules
- **All speech through `Speech.Say`** (Core) -> the host `SpeechPipeline`; never call Prism directly.
  Navigation moves interrupt; screen entry and feedback queue.
- **Never cache game state.** Hold live widgets and read them at speak time.
- **Reuse game text** wherever it exists; every mod-authored word lives in `Core/Strings/Strings.cs`
  (keyed, overridable by `lang/strings.txt`), never inline in a Speak call.
- **No silent failures**: every catch logs through `CoreLog` / the host logger.
- **Announcements** (mod-authored text only; never reword game text): distinguishing word first, no
  nav hints, no fluff, never less information. Users are expert screen-reader users: no "list" role,
  no positions unless the player turns them on.
- **Private game fields through `Interop/Private.Get<T>(obj, "name")`**: cached per generation, a
  missing name logs once (the trace of a game update renaming it) and reads as the default. Public
  fields and properties are used directly.
- **No scene scans** in the module: reach widgets from a singleton's fields or a page object's
  children (`transform.Find` by the hierarchy's names, checked against `/gui`).
- In Module files with `using UnityEngine;`, alias `Screen` and `Navigation` to the Core types
  (`UnityEngine.Screen.fullScreen` spelled out where the engine's is meant).
- Beware `int.MinValue` "long ago" sentinels: `frameCount - int.MinValue` overflows negative and
  disables a throttle forever. Use `-Interval` or `int.MinValue / 2`.

## The game's own hotkeys
The game's PlayerInput binds the arrows, Enter, Escape, Tab and letters (navigate, submit, back, the
drawers, refresh, typing). While focus mode is on, `FocusMode` disables the keyboard as an Input
System DEVICE (`InputSystem.DisableDevice`), so no game action hears a key; our own keys are polled
through the legacy input path, which is unaffected. `InputSystem.onDeviceChange` re-disables a keyboard
the moment anything enables it again. Turning focus mode off re-enables it. Anything a game hotkey did
must be offered through our screens instead. The round is the exception: it lends the keyboard (the
game's own keys and typing work, its clashing actions disabled by name; see The game scene). A side
effect worth knowing: with the keyboard device off
the game never switches `ControlManagerScript.currentControlType` to Keyboard, so its controller arrow
stays hidden and the mouse cursor shown.

## Roadmap
1. **(done)** Loader bring-up on Mono (BepInEx 5), host + per-generation renamed hot reload, Prism,
   the dev server with Roslyn eval.
2. **(done)** Graph engine + navigator + input substrate + buffers + key help in Core, ported from
   guildrun with its tests (140 now); Tab always wraps, no list role, positions off by default.
3. **(done)** The title scene: main menu, level select, options, extras, consent.
4. **(done)** The round's core: keyboard lending, the board with coordinates, the word, status,
   events, the bag, the Options drawer, the tutorial panels.
5. Next in the round: the shop (between rounds; the game moves its zone to Shop and the round screen
   steps aside), the Modifier and Upgrade drawers (stops of the round once enabled), click-to-upgrade
   and the Swap Tile picker, moving a tile within the word, the end screen (`GameOverScreen`), the
   word log buffer.
6. Releases: version in `Directory.Build.props` (`0.0.1`), `changelog.md`, the release zip, the
   installer and the GitHub release script are done (see Build & deploy); the manual is still to
   come.
