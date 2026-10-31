"""Check overlay profiles; optionally verify every rectangle against read-only .Lib headers."""
import argparse
import json
import struct
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
parser = argparse.ArgumentParser()
parser.add_argument("--assets", type=Path, help="Client Data directory (optional; never modified)")
args = parser.parse_args()
rules = json.loads((ROOT / "Client/Localization/Overlay.zh-CN.json").read_text(encoding="utf-8-sig"))["Rules"]
dimensions = {}
if args.assets:
    for lib in {r["Library"] for r in rules}:
        path = next(p for p in args.assets.iterdir() if p.name.lower() == lib.lower() + ".lib")
        with path.open("rb") as stream:
            version, count = struct.unpack("<ii", stream.read(8))
            assert version in (2, 3), (lib, version)
            if version >= 3:
                stream.read(4)
            offsets = struct.unpack("<" + "i" * count, stream.read(4 * count))
            for index in {i for r in rules if r["Library"] == lib for i in r["Indices"]}:
                assert 0 <= index < count, (lib, index)
                stream.seek(offsets[index])
                dimensions[(lib, index)] = struct.unpack("<hh", stream.read(4))
seen = set()
regions = 0
for rule in rules:
    assert rule["Library"] in {"Title", "Prguse", "Prguse2", "Prguse3"}
    assert rule["Indices"] and rule["Rects"]
    for index in rule["Indices"]:
        key = (rule["Library"], index, rule.get("ControlType"), rule.get("ParentType"))
        assert key not in seen, ("ambiguous repeated profile", key)
        seen.add(key)
    for region in rule["Rects"]:
        x, y, width, height = region["Rect"]
        assert x >= 0 and y >= 0 and width > 0 and height > 0 and region["Text"], region
        assert 6 <= region.get("FontSize", 8.5) <= 16, region
        for index in rule["Indices"]:
            if dimensions:
                w, h = dimensions[(rule["Library"], index)]
                assert x + width <= w and y + height <= h, (rule["Library"], index, (w, h), region)
        if rule["Library"] == "Title" and 504 in rule["Indices"]:
            assert y >= 70, ("character name/guild header must stay uncovered", region)
        if rule["Library"] == "Title" and any(i in (506, 507) for i in rule["Indices"]):
            assert x + width <= 120, ("character numbers start at x126", region)
        regions += 1
print(f"PASS: {len(rules)} profiles, {regions} regions, {len(seen)} indexed/context mappings.")
print("All resource rectangles fit read-only .Lib headers." if dimensions else "Resource dimensions not checked; supply --assets to verify the installed art.")
