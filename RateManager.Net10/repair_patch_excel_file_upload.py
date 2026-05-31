#!/usr/bin/env python3
# -*- coding: utf-8 -*-

# repair_patch_excel_file_upload.py
#
# Put this file beside patch_excel_file_upload.py, then run:
#   python repair_patch_excel_file_upload.py
#   python patch_excel_file_upload.py
#
# It fixes the Python f-string escaping issue around this C# line:
#   var safeFileName = $"{Guid.NewGuid():N}{extension}";

from pathlib import Path


def main() -> int:
    script_path = Path("patch_excel_file_upload.py")

    if not script_path.exists():
        print("ERROR: patch_excel_file_upload.py was not found in the current folder.")
        print("Put this repair file in the same folder as patch_excel_file_upload.py.")
        return 1

    text = script_path.read_text(encoding="utf-8-sig")
    original = text

    replacements = {
        'var safeFileName = $"{Guid.NewGuid():N}{{extension}}";':
            'var safeFileName = $"{{Guid.NewGuid():N}}{{extension}}";',

        'var safeFileName = $"{Guid.NewGuid():N}{extension}";':
            'var safeFileName = $"{{Guid.NewGuid():N}}{{extension}}";',
    }

    for old, new in replacements.items():
        text = text.replace(old, new)

    if text == original:
        print("No change was needed, or the problematic line was not found.")
        print("You can still try running: python patch_excel_file_upload.py")
        return 0

    backup_path = Path("patch_excel_file_upload.py.bak2")
    if not backup_path.exists():
        backup_path.write_text(original, encoding="utf-8")

    script_path.write_text(text, encoding="utf-8")

    print("Fixed patch_excel_file_upload.py successfully.")
    print("Now run:")
    print("python patch_excel_file_upload.py")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
