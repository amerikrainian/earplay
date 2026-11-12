"""Shared helpers for the tools: where the game, the decompiled reference and the dev server live.

Run every tool with uv (`uv run python tools/python/<tool>.py ...`); they need only the standard
library. The game directory resolves the same way the build does: WORDPLAY_DIR, then Steam's
library from the registry, then the default install path.
"""
from __future__ import annotations

import os
import re
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
GAME_REF = REPO / "game"
DECOMPILED = GAME_REF / "decompiled"
VERSION_JSON = GAME_REF / "analysis" / "version.json"
MODULE_SRC = REPO / "src" / "Earplay.Module"
STEAM_APP_ID = "3586660"
GAME_FOLDER = "Word Play"
GAME_EXE = "Word Play.exe"
DEV_URL = os.environ.get("EARPLAY_DEV_URL", "http://127.0.0.1:" + os.environ.get("EARPLAY_DEV_PORT", "8772"))


def game_dir() -> Path:
    """The game install, found the way Directory.Build.props finds it."""
    env = os.environ.get("WORDPLAY_DIR")
    if env and Path(env).is_dir():
        return Path(env)
    for hive, key, value in (
        ("HKEY_CURRENT_USER", r"Software\Valve\Steam", "SteamPath"),
        ("HKEY_LOCAL_MACHINE", r"SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath"),
        ("HKEY_LOCAL_MACHINE", r"SOFTWARE\Valve\Steam", "InstallPath"),
    ):
        try:
            import winreg  # Windows only

            root = getattr(winreg, hive)
            with winreg.OpenKey(root, key) as k:
                steam, _ = winreg.QueryValueEx(k, value)
            candidate = Path(steam) / "steamapps" / "common" / GAME_FOLDER
            if candidate.is_dir():
                return candidate
        except OSError:
            continue
    default = Path(r"C:\Program Files (x86)\Steam\steamapps\common") / GAME_FOLDER
    if default.is_dir():
        return default
    sys.exit("Word Play not found: set WORDPLAY_DIR")


def managed_dir(game: Path | None = None) -> Path:
    """The game's Mono assemblies (Assembly-CSharp and the Unity modules)."""
    return (game or game_dir()) / "Word Play_Data" / "Managed"


def steam_build_id(game: Path | None = None) -> str | None:
    """Steam's build id for the installed game (changes with every game update)."""
    game = game or game_dir()
    manifest = game.parents[1] / f"appmanifest_{STEAM_APP_ID}.acf"
    try:
        text = manifest.read_text(encoding="utf-8", errors="replace")
    except OSError:
        return None
    m = re.search(r'"buildid"\s+"(\d+)"', text)
    return m.group(1) if m else None
