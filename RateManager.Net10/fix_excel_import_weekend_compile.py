#!/usr/bin/env python3
# -*- coding: utf-8 -*-

# fix_excel_import_weekend_compile.py
#
# Run from the ASP.NET Core project folder:
#   cd C:\Users\Gafoor\Desktop\RateManager.Net10\RateManager.Net10
#   python fix_excel_import_weekend_compile.py
#
# This fixes compile errors caused by ExcelRateImportService.cs referencing:
#   _db.WeekendDaySettings
#   DailyRoomRate.IsWeekend
#
# It removes those references so the Excel upload/import feature works with your current project version.

from __future__ import annotations

import re
from pathlib import Path


def read_text(path: Path) -> str:
    return path.read_text(encoding="utf-8-sig")


def write_text(path: Path, text: str) -> None:
    path.write_text(text, encoding="utf-8")


def backup(path: Path) -> None:
    bak = path.with_suffix(path.suffix + ".bak_weekend_compile")
    if not bak.exists():
        bak.write_text(read_text(path), encoding="utf-8")


def find_project_root(start: Path) -> Path | None:
    current = start.resolve()

    for folder in [current, *current.parents]:
        if list(folder.glob("*.csproj")):
            return folder

    for candidate in current.glob("*/*.csproj"):
        return candidate.parent

    for candidate in current.glob("*/*/*.csproj"):
        return candidate.parent

    return None


def patch_excel_service(project_root: Path) -> bool:
    service_path = project_root / "Services" / "ExcelRateImportService.cs"

    if not service_path.exists():
        print(f"ERROR: File not found: {service_path}")
        return False

    text = read_text(service_path)
    original = text

    # Remove the weekendDays database query block.
    text = re.sub(
        r"\n\s*var\s+weekendDays\s*=\s*await\s+_db\.WeekendDaySettings\s*\n"
        r"\s*\.Where\(x\s*=>\s*x\.IsWeekend\)\s*\n"
        r"\s*\.Select\(x\s*=>\s*x\.Weekday\)\s*\n"
        r"\s*\.ToListAsync\(\);\s*\n",
        "\n",
        text,
        flags=re.MULTILINE,
    )

    # Remove IsWeekend assignment inside DailyRoomRate initializer.
    text = re.sub(
        r"\n\s*IsWeekend\s*=\s*weekendDays\.Contains\(dateTime\.DayOfWeek\),\s*",
        "\n",
        text,
        flags=re.MULTILINE,
    )

    # Some variants may be a one-line block.
    text = re.sub(
        r"\n\s*var\s+weekendDays\s*=\s*await\s+_db\.WeekendDaySettings.*?\.ToListAsync\(\);\s*",
        "\n",
        text,
        flags=re.DOTALL,
    )

    text = re.sub(
        r"\n\s*IsWeekend\s*=\s*[^,\n]+,\s*",
        "\n",
        text,
        flags=re.MULTILINE,
    )

    if text == original:
        print("No changes were needed in ExcelRateImportService.cs.")
        return False

    backup(service_path)
    write_text(service_path, text)
    print("Updated ExcelRateImportService.cs successfully.")
    return True


def main() -> int:
    project_root = find_project_root(Path.cwd())
    if project_root is None:
        print("ERROR: Could not find a .csproj file.")
        print("Run this script from the ASP.NET Core project folder.")
        return 1

    print("Excel import weekend compile fixer")
    print("=" * 40)
    print(f"Project root: {project_root}")

    changed = patch_excel_service(project_root)

    print()
    if changed:
        print("Now rebuild the project in Visual Studio.")
    else:
        print("Nothing was changed. If the error is still there, open ExcelRateImportService.cs and search for WeekendDaySettings or IsWeekend.")

    print("Done.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
