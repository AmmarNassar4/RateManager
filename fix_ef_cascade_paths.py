#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
fix_ef_cascade_paths.py

Run this file from the root folder of the Visual Studio project, for example:

    cd C:\Users\Gafoor\Desktop\RateManager.Net10\RateManager.Net10
    python fix_ef_cascade_paths.py

What it does:
1) Finds Data/AppDbContext.cs.
2) Adds a global EF Core rule to disable Cascade Delete:
       foreignKey.DeleteBehavior = DeleteBehavior.NoAction;
3) Updates migration files, if any, by replacing ReferentialAction.Cascade with ReferentialAction.NoAction.
4) Creates .bak backup files before editing.

After running:
- Delete the partially-created database, or use a fresh database name.
- Run the app again.

Important:
This script fixes the project files. It does not connect to SQL Server.
"""

from __future__ import annotations

import re
import sys
from pathlib import Path


GLOBAL_NO_ACTION_CODE = """
        // Disable cascade delete globally for SQL Server.
        // This prevents "multiple cascade paths" errors when EF creates the database.
        foreach (var foreignKey in modelBuilder.Model.GetEntityTypes().SelectMany(entityType => entityType.GetForeignKeys()))
        {
            foreignKey.DeleteBehavior = DeleteBehavior.NoAction;
        }
""".rstrip()


def read_text(path: Path) -> str:
    return path.read_text(encoding="utf-8-sig")


def write_text(path: Path, text: str) -> None:
    path.write_text(text, encoding="utf-8")


def backup(path: Path) -> Path:
    bak = path.with_suffix(path.suffix + ".bak")
    if not bak.exists():
        bak.write_text(read_text(path), encoding="utf-8")
    return bak


def find_app_db_context(root: Path) -> Path | None:
    candidates = list(root.rglob("AppDbContext.cs"))
    if not candidates:
        return None

    # Prefer Data/AppDbContext.cs
    for candidate in candidates:
        parts = {p.lower() for p in candidate.parts}
        if "data" in parts:
            return candidate

    return candidates[0]


def ensure_linq_using(text: str) -> str:
    if "using System.Linq;" in text:
        return text

    # Add after last using line at the top
    matches = list(re.finditer(r"^using\s+[^;]+;\s*$", text, flags=re.MULTILINE))
    if not matches:
        return "using System.Linq;\n" + text

    last = matches[-1]
    return text[: last.end()] + "\nusing System.Linq;" + text[last.end():]


def has_global_no_action(text: str) -> bool:
    return (
        "Disable cascade delete globally for SQL Server" in text
        or "foreignKey.DeleteBehavior = DeleteBehavior.NoAction" in text
    )


def find_method_body_bounds(text: str, method_name: str) -> tuple[int, int] | None:
    method_match = re.search(
        rf"(protected|public|private)\s+override\s+void\s+{re.escape(method_name)}\s*\([^)]*\)\s*",
        text,
    )
    if not method_match:
        return None

    open_brace = text.find("{", method_match.end())
    if open_brace == -1:
        return None

    depth = 0
    for i in range(open_brace, len(text)):
        char = text[i]
        if char == "{":
            depth += 1
        elif char == "}":
            depth -= 1
            if depth == 0:
                return open_brace, i

    return None


def patch_on_model_creating(text: str) -> tuple[str, bool]:
    if has_global_no_action(text):
        return text, False

    bounds = find_method_body_bounds(text, "OnModelCreating")
    if not bounds:
        return text, False

    open_brace, close_brace = bounds

    insertion = "\n" + GLOBAL_NO_ACTION_CODE + "\n"
    new_text = text[:close_brace] + insertion + text[close_brace:]
    return new_text, True


def patch_app_db_context(path: Path) -> bool:
    text = read_text(path)
    original = text

    text = ensure_linq_using(text)
    text, changed_model = patch_on_model_creating(text)

    if text == original:
        return False

    backup(path)
    write_text(path, text)
    return True


def patch_migrations(root: Path) -> int:
    changed_count = 0

    for path in root.rglob("*.cs"):
        if "migrations" not in {p.lower() for p in path.parts}:
            continue

        text = read_text(path)
        new_text = text.replace("ReferentialAction.Cascade", "ReferentialAction.NoAction")

        if new_text != text:
            backup(path)
            write_text(path, new_text)
            changed_count += 1

    return changed_count


def main() -> int:
    root = Path.cwd()

    print("EF Core cascade-path fixer")
    print("=" * 32)
    print(f"Project folder: {root}")

    app_db_context = find_app_db_context(root)
    if not app_db_context:
        print("ERROR: Could not find AppDbContext.cs under this folder.")
        print("Run this script from the project root folder, not from Downloads or another location.")
        return 1

    print(f"Found AppDbContext: {app_db_context}")

    try:
        app_changed = patch_app_db_context(app_db_context)
        migrations_changed = patch_migrations(root)
    except Exception as exc:
        print(f"ERROR while patching files: {exc}")
        return 1

    if app_changed:
        print("Updated AppDbContext.cs: added global DeleteBehavior.NoAction.")
    else:
        print("AppDbContext.cs already appears to contain the NoAction fix.")

    if migrations_changed:
        print(f"Updated {migrations_changed} migration file(s): Cascade -> NoAction.")
    else:
        print("No migration files needed updating, or no migrations were found.")

    print()
    print("Next steps:")
    print("1) If the database was partially created, drop it from SQL Server.")
    print("   Example:")
    print("   DROP DATABASE RateManagerDb;")
    print("2) Run the project again from Visual Studio.")
    print()
    print("Done.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
