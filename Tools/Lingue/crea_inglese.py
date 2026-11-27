#!/usr/bin/env python3
"""Scrive Assets/_Project/Horror/Resources/Lingue/en.txt (italiano<TAB>inglese) dalle battute e dalle scritte tradotte."""
import json, os, sys
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from traduzioni_en import BATTUTE, EXTRA, UI
index = json.load(open(os.path.join(ROOT, "Tools/Battute/battute_indice.json"), encoding="utf-8"))
pairs = {}
for e in index:
    it = e["text"].replace('\\"', '"')
    if e["n"] in BATTUTE: pairs[it] = BATTUTE[e["n"]]
pairs.update(EXTRA); pairs.update(UI)
def flat(s): return s.replace("\n", "\\n").replace("\t", " ")
out = ["# italiano<TAB>inglese  (generato da Tools/Lingue/crea_inglese.py)"]
for it, en in pairs.items(): out.append(flat(it) + "\t" + flat(en))
p = os.path.join(ROOT, "Assets/_Project/Horror/Resources/Lingue/en.txt")
open(p, "w", encoding="utf-8").write("\n".join(out) + "\n")
print(len(pairs), "frasi tradotte ->", p)
missing = [e["n"] for e in index if e["n"] not in BATTUTE]
if missing: print("battute senza traduzione:", missing)
