"""
Generates src/HotkeyLauncher/Resources/Strings*.resx from tools/strings.tsv.

strings.tsv: one line per text, columns separated by TABs:
    key <TAB> en <TAB> fr <TAB> es <TAB> de ...
The first line is the header and gives the language codes. "en" is the neutral language (Strings.resx).
"\\n" in a text is a line break.

To add a language: add a column (and declare the language in Localizer.Languages and in
SatelliteResourceLanguages of Directory.Build.props), then run:
    python tools/make_resx.py
"""
import os
import sys
from xml.sax.saxutils import escape

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
TSV = os.path.join(ROOT, "tools", "strings.tsv")
OUT = os.path.join(ROOT, "src", "HotkeyLauncher", "Resources")

HEADER = """<?xml version="1.0" encoding="utf-8"?>
<!-- Generated from tools/strings.tsv by tools/make_resx.py: edit the TSV, not this file. -->
<root>
  <resheader name="resmimetype"><value>text/microsoft-resx</value></resheader>
  <resheader name="version"><value>2.0</value></resheader>
  <resheader name="reader"><value>System.Resources.ResXResourceReader, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value></resheader>
  <resheader name="writer"><value>System.Resources.ResXResourceWriter, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value></resheader>
"""


def main():
    with open(TSV, encoding="utf-8") as f:
        lines = [l.rstrip("\r\n") for l in f if l.strip()]

    languages = lines[0].split("\t")[1:]
    rows = []
    for number, line in enumerate(lines[1:], start=2):
        cells = line.split("\t")
        if len(cells) != len(languages) + 1:
            sys.exit(f"strings.tsv line {number}: {len(cells)} columns, {len(languages) + 1} expected")
        rows.append((cells[0], [c.replace("\\n", "\n") for c in cells[1:]]))

    keys = [r[0] for r in rows]
    duplicates = sorted({k for k in keys if keys.count(k) > 1})
    if duplicates:
        sys.exit(f"duplicate keys: {duplicates}")

    for index, language in enumerate(languages):
        suffix = "" if language == "en" else f".{language}"
        path = os.path.join(OUT, f"Strings{suffix}.resx")
        with open(path, "w", encoding="utf-8", newline="\r\n") as f:
            f.write(HEADER)
            for key, texts in rows:
                f.write(f'  <data name="{key}" xml:space="preserve"><value>{escape(texts[index])}</value></data>\n')
            f.write("</root>\n")

    print(f"{len(rows)} strings x {len(languages)} languages ({', '.join(languages)})")


if __name__ == "__main__":
    main()
