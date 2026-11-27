#!/usr/bin/env python3
"""
Rimette nel codice le battute modificate in Tools/Battute/Battute.txt (usando battute_indice.json creato
dall'esportazione). Per ogni battuta cambiata sostituisce il testo nel file C# (vicino alla riga indicata).
Dopo, vanno rigenerate le voci (Tools/Voci/genera_voci.py) e riesportate le battute.
"""
import json, os, re, sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
HERE = os.path.join(ROOT, "Tools/Battute")

def esc(s): return s.replace("\\", "\\\\").replace('"', '\\"')

def main():
    index = {e["n"]: e for e in json.load(open(os.path.join(HERE, "battute_indice.json"), encoding="utf-8"))}
    path = sys.argv[1] if len(sys.argv) > 1 else os.path.join(HERE, "Battute.txt")
    edited = {}
    for raw in open(path, encoding="utf-8"):
        m = re.match(r"\s*\[(\d+)\][^:]*:\s?(.*)$", raw.rstrip("\n"))
        if m: edited[int(m.group(1))] = m.group(2).strip().replace(" / ", "\\n")
    changes = 0
    files = {}
    for n, new in sorted(edited.items()):
        e = index.get(n)
        if e is None: continue
        old = e["text"]
        if new == old.replace('\\"', '"'): continue
        f = e["file"]
        if f not in files: files[f] = open(os.path.join(ROOT, f), encoding="utf-8").read().split("\n")
        lines = files[f]
        target = old  # già scritto come nel sorgente (con \")
        done = False
        for d in sorted(range(-3, 4), key=abs):
            i = e["line"] - 1 + d
            if 0 <= i < len(lines) and target in lines[i]:
                lines[i] = lines[i].replace(target, esc(new.replace("\\n", "\n")).replace("\n", "\\n"), 1)
                done = True
                break
        print(("cambiata" if done else "NON TROVATA"), f"[{n:03d}]", old, "->", new)
        changes += done
    for f, lines in files.items():
        open(os.path.join(ROOT, f), "w", encoding="utf-8").write("\n".join(lines))
    print(changes, "battute cambiate")

if __name__ == "__main__":
    main()
