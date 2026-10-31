"""Validate curated player prose without requiring the private game database."""
import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
PATTERNS = {
    "menu targets": r"/@[^<>]+",
    "variables": r"<\$[^<>]+>",
    "object links": r"\[(?:ITEM|NPC|MONSTER):[^\]]*\]",
    "format placeholders": r"\{\d+(?:,\s*-?\d+)?(?::[^{}]*)?\}",
    "colours": r"/([A-Za-z]+)\}",
    "commands": r"@[A-Za-z][A-Za-z0-9_]*",
}
count = 0
exceptions = []
data = json.loads((ROOT / "Localization/NpcNarrative.zh-CN.json").read_text(encoding="utf-8-sig"))
for category in ("NpcLines", "Tooltips", "Ui", "QuestGroups"):
    for original, translated in data[category].items():
        assert translated, (category, original, "empty translation")
        for kind, pattern in PATTERNS.items():
            before, after = re.findall(pattern, original), re.findall(pattern, translated)
            # Chinese may move the location before the character's name; keep
            # each noun's colour attached while allowing natural word order.
            if kind == "colours":
                before, after = sorted(before), sorted(after)
            assert before == after, (category, original, kind)
        for char in ("\r", "\n", "\t", "\\", "<", ">", "{", "}"):
            assert original.count(char) == translated.count(char), (category, original, repr(char))
        if category in ("NpcLines", "Tooltips"):
            assert re.search(r"\s*$", original)[0] == re.search(r"\s*$", translated)[0], (original, "trailing whitespace")
        if original == translated:
            exceptions.append(original)
        count += 1
assert set(exceptions) <= {"MOVE 11 176 327", "MOVE 3 330 330", "SET [526] 1", "[@before1>"}, exceptions
print(f"PASS: {count} player prose/UI entries; script targets, variables, colours, links, formatting and escapes preserved.")
print(f"Preserved source commands/malformed marker: {len(exceptions)}")
