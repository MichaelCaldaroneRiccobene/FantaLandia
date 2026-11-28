#!/usr/bin/env python3
"""
Esporta tutte le battute del gioco in un file di testo da leggere e modificare (Tools/Battute/Battute.txt),
nell'ordine in cui compaiono nel codice, divise per file/scena. Ogni battuta ha un numero [123]:
si cambia solo il testo dopo i due punti, lasciando numero e nome. Il file battute_indice.json tiene
traccia di dove sta ogni battuta nel codice, per rimettere i testi modificati al loro posto.
"""
import json, os, re

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT = os.path.join(ROOT, "Tools/Battute")
FILES = [
    ("Capitolo 1 - la storia", "Assets/_Project/Horror/Scripts/Story/StoryDirector.cs"),
    ("Cartone in TV: Lucia e il buio", "Assets/_Project/Horror/Scripts/World/CartoonEpisode.cs"),
    ("Vicini (prima dell'esplosione)", "Assets/_Project/Horror/Editor/HouseSceneBuilder.cs"),
    ("Richiami quando ti allontani", "Assets/_Project/Horror/Scripts/Dialogue/DialogueSystem.cs"),
    ("Quando stai addosso a qualcuno", "Assets/_Project/Horror/Scripts/Story/NpcActor.cs"),
    ("Generatore", "Assets/_Project/Horror/Scripts/Interaction/GeneratorStarter.cs"),
    ("Capitolo 2 - la storia", "Assets/_Project/Horror/Scripts/Story/Chapter2Director.cs"),
    ("Capitolo 2 - la radio in cantina", "Assets/_Project/Horror/Scripts/World/RadioTuner.cs"),
]
NAMES = {"muark": "Muark", "tom": "Tom", "jef": "Jef", "roy": "Roy", "tv": "TV", "frank": "Frank", "martha": "Martha", "gary": "Gary",
         "linda": "Linda", "vicino": "Vicino", "vicina": "Vicina", "lucia": "Lucia", "tasso": "Tasso", "gufo": "Gufo", "rana": "Rana",
         "radio": "Radio", "radioamatore": "Radioamatore", "militare": "Militare", "fattorino": "Fattorino"}
FIELD = {"Muark": "muark", "Tom": "tom", "Jef": "jef", "Roy": "roy", "Tv": "tv", "Frank": "frank", "Martha": "martha", "Gary": "gary", "Linda": "linda",
         "Radio": "radio", "Ham": "radioamatore", "Military": "militare", "PizzaBoy": "fattorino"}
STR = r'"((?:[^"\\]|\\.)*)"'

def unesc(s): return s.replace('\\"', '"').replace("\\n", " / ")

def scan(path):
    src = open(os.path.join(ROOT, path), encoding="utf-8").read()
    found = []  # (pos, speaker, text, kind)
    for m in re.finditer(r"Speakers\.(\w+)\s*,\s*" + STR, src):
        if m.group(1) in FIELD: found.append((m.start(2), FIELD[m.group(1)], m.group(2), "battuta"))
    for m in re.finditer(r'"(' + "|".join(NAMES) + r')\|((?:[^"\\]|\\.)*)"', src):
        found.append((m.start(2), m.group(1), m.group(2), "battuta"))
    for m in re.finditer(r"\.Choice\(new\[\]\s*\{([^}]*)\}", src):
        for c in re.finditer(STR, m.group(1)):
            found.append((m.start(1) + c.start(1), "muark", c.group(1), "scelta"))
    for arr, who in [(r"tomPlays\s*=\s*\{([^}]*)\}", "tom"), (r"FailLines\s*=\s*\{([^}]*)\}", "muark"),
                     (r"Nags\s*=\s*\{([^}]*)\}", "amici"), (r"string\[\] lines\s*=\s*\{([^}]*)\}", "amici")]:
        for m in re.finditer(arr, src):
            for c in re.finditer(STR, m.group(1)):
                found.append((m.start(1) + c.start(1), who, c.group(1), "battuta"))
    for m in re.finditer(r'Objective\s*=\s*' + STR, src):
        if m.group(1): found.append((m.start(1), "taccuino", m.group(1), "obiettivo"))
    found.sort()
    seen, out = set(), []
    for pos, who, text, kind in found:
        if pos in seen: continue
        seen.add(pos)
        line = src.count("\n", 0, pos) + 1
        out.append((who, text, kind, line))
    return out

def main():
    index, lines, n = [], [], 0
    lines.append("BATTUTE DI FANTALANDIA - capitoli 1 e 2")
    lines.append("Cambia solo il testo dopo i due punti. Lascia il numero [..] e il nome. Le righe che non tocchi restano uguali.")
    lines.append("SCELTA = una risposta che il giocatore sceglie (la dice Muark). OBIETTIVO = cosa è scritto nel taccuino.")
    lines.append("")
    for title, path in FILES:
        items = scan(path)
        if not items: continue
        lines.append("=" * 70)
        lines.append(title.upper())
        lines.append("=" * 70)
        for who, text, kind, line in items:
            n += 1
            label = NAMES.get(who, who.capitalize())
            tag = " (SCELTA)" if kind == "scelta" else " (OBIETTIVO)" if kind == "obiettivo" else ""
            lines.append(f"[{n:03d}] {label}{tag}: {unesc(text)}")
            index.append({"n": n, "file": path, "line": line, "speaker": who, "text": text, "kind": kind})
        lines.append("")
    open(os.path.join(OUT, "Battute.txt"), "w", encoding="utf-8").write("\n".join(lines) + "\n")
    json.dump(index, open(os.path.join(OUT, "battute_indice.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=1)
    print(n, "battute")

if __name__ == "__main__":
    main()
