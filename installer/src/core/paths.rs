// Adapted from the Non-Visual Calculus installer by Rashad Naqeeb (MIT),
// https://github.com/rashadnaqeeb/NonVisualCalculus

use std::path::{Path, PathBuf};

// The full release list, newest first: one call serves both the latest-version
// lookup and the per-version release notes shown after an update.
pub const GITHUB_RELEASES_URL: &str =
    "https://api.github.com/repos/amerikrainian/earplay/releases?per_page=100";
pub const MOD_ZIP_PREFIX: &str = "Earplay-v";
pub const MOD_ZIP_SUFFIX: &str = ".zip";
pub const GAME_EXES: &[&str] = &["Word Play.exe"];
// Steam's install folder (app 3586660).
pub const GAME_FOLDERS: &[&str] = &["Word Play"];
// The game is Mono: its code is Assembly-CSharp.dll in the data folder's Managed dir, in every
// install, so together with the exe it identifies the game dir.
pub const GAME_ASSEMBLY_MARKER: &str = "Word Play_Data/Managed/Assembly-CSharp.dll";
pub const PLUGIN_REL: &str = "BepInEx/plugins/Earplay/Earplay.dll";
pub const MANIFEST_REL: &str = "BepInEx/config/Earplay/install.json";
pub const BACKUPS_REL: &str = "BepInEx/config/Earplay/backups";

pub fn manifest_path(game_dir: &Path) -> PathBuf {
    game_dir.join(MANIFEST_REL)
}

pub fn normalize_rel(path: &str) -> String {
    path.replace('\\', "/").trim_start_matches("./").to_string()
}

pub fn required_loader_files() -> &'static [&'static str] {
    &[
        "winhttp.dll",
        "doorstop_config.ini",
        "BepInEx/core/BepInEx.dll",
        PLUGIN_REL,
    ]
}
