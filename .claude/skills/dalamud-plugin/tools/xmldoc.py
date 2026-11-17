#!/usr/bin/env python3
"""Search the XML API docs that ship with Dalamud.

Usage: python3 -I xmldoc.py <File.xml> <regex> [<regex> ...]

Prints every documented member whose id matches one of the regexes, with its full doc text. Member ids look like
  T:Dalamud.Plugin.Services.INamePlateGui            (type)
  P:Dalamud.Game.Gui.NamePlate.INamePlateUpdateHandler.NameIconId   (property)
  M:...Method(Arg.Type,Other.Type)   E:... event   F:... field
The file is looked up in $DALAMUD_HOME, else ~/.xlcore/dalamud/Hooks/dev, unless a path is given.

Note: the ImGui bindings ship no XML docs; use tools/apidump for those.
"""
import os
import re
import sys
import xml.etree.ElementTree as ET


def main() -> None:
    if len(sys.argv) < 3:
        sys.exit(__doc__)

    path = sys.argv[1]
    if not os.path.exists(path):
        home = os.environ.get("DALAMUD_HOME") or os.path.expanduser("~/.xlcore/dalamud/Hooks/dev")
        path = os.path.join(home, path)

    patterns = [re.compile(p) for p in sys.argv[2:]]
    for member in ET.parse(path).getroot().iter("member"):
        name = member.get("name", "")
        if any(p.search(name) for p in patterns):
            # itertext() keeps the text inside <see cref>, <paramref> etc. that findtext() would drop.
            text = " ".join("".join(member.itertext()).split())
            print(f"{name}\n    {text[:1200]}\n")


if __name__ == "__main__":
    main()
