"""Decompile the game's code into game/decompiled (gitignored) and record which build it came from.

    uv run python tools/python/dump_game.py [--keep] [--diff]

Word Play is a Mono game: its code is plain IL in Word Play_Data/Managed/Assembly-CSharp.dll, so
ilspycmd (`dotnet tool install -g ilspycmd`) recovers whole method bodies, one .cs file per type.
Grep the result; it is the reference for every type, field and rule the module reads.

  --keep   park the previous decompile as game/decompiled.prev before writing the new one
  --diff   after decompiling, list the files that a game update added, removed or changed
           (against game/decompiled.prev; implies --keep)

game/analysis/version.json records the Steam build id and Assembly-CSharp's write time, so a stale
reference is visible. Decompiled game code is never committed.
"""
from __future__ import annotations

import argparse
import filecmp
import json
import shutil
import subprocess
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import gamelib  # noqa: E402


def decompile(managed: Path, out: Path) -> None:
    ilspy = shutil.which("ilspycmd")
    if not ilspy:
        sys.exit("ilspycmd not found: dotnet tool install -g ilspycmd")
    out.mkdir(parents=True, exist_ok=True)
    cmd = [ilspy, "-p", "-o", str(out), str(managed / "Assembly-CSharp.dll"), "-r", str(managed)]
    print("decompiling Assembly-CSharp ...", flush=True)
    result = subprocess.run(cmd, capture_output=True, text=True)
    if result.returncode != 0:
        sys.exit("ilspycmd failed:\n" + result.stdout + result.stderr)


def diff(old: Path, new: Path) -> None:
    def files(root: Path) -> set[str]:
        return {p.relative_to(root).as_posix() for p in root.rglob("*.cs")}

    before, after = files(old), files(new)
    added, removed = sorted(after - before), sorted(before - after)
    changed = sorted(f for f in before & after if not filecmp.cmp(old / f, new / f, shallow=False))
    for title, items in (("added", added), ("removed", removed), ("changed", changed)):
        print(f"{title} ({len(items)}):")
        for item in items:
            print(f"  {new / item if title != 'removed' else old / item}")


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--keep", action="store_true")
    ap.add_argument("--diff", action="store_true")
    args = ap.parse_args()

    game = gamelib.game_dir()
    managed = gamelib.managed_dir(game)
    prev = gamelib.GAME_REF / "decompiled.prev"
    if gamelib.DECOMPILED.exists():
        if args.keep or args.diff:
            shutil.rmtree(prev, ignore_errors=True)
            gamelib.DECOMPILED.rename(prev)
        else:
            shutil.rmtree(gamelib.DECOMPILED)

    decompile(managed, gamelib.DECOMPILED)

    dll = managed / "Assembly-CSharp.dll"
    gamelib.VERSION_JSON.parent.mkdir(parents=True, exist_ok=True)
    gamelib.VERSION_JSON.write_text(json.dumps({
        "steam_build_id": gamelib.steam_build_id(game),
        "assembly_csharp_mtime": dll.stat().st_mtime,
        "assembly_csharp_size": dll.stat().st_size,
    }, indent=2) + "\n", encoding="utf-8")
    count = sum(1 for _ in gamelib.DECOMPILED.rglob("*.cs"))
    print(f"{count} files in {gamelib.DECOMPILED} (build {gamelib.steam_build_id(game)})")

    if args.diff and prev.exists():
        diff(prev, gamelib.DECOMPILED)
    return 0


if __name__ == "__main__":
    sys.exit(main())
