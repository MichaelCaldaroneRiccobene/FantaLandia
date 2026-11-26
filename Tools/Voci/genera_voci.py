#!/usr/bin/env python3
"""
Genera le voci italiane dei dialoghi (FantaLandia Horror).

Legge tutte le battute dal codice C# (Assets/_Project/Horror/Scripts), le fa leggere a una voce neurale italiana
(Coqui TTS, modelli VITS "mai" maschile e femminile) e crea un file .ogg per ogni battuta in
Assets/_Project/Horror/Resources/Voci/<personaggio>_<hash>.ogg. Il gioco cerca la voce con lo stesso nome
(hash MD5 del testo) e, se non c'è, usa il borbottio "alla Animal Crossing".

Ogni personaggio ha la sua voce: stesso modello, ma tono e velocità diversi (Jef lento e grave, Roy acuto e veloce,
la TV filtrata come un altoparlante).

Uso (serve Python 3, ffmpeg e:  pip install torch "coqui-tts[codec]" six):
    python Tools/Voci/genera_voci.py --models <cartella con it_male/ e it_female/>
I modelli si scaricano da:
    https://github.com/coqui-ai/TTS/releases/download/v0.6.1_models/tts_models--it--mai_male--vits.zip
    https://github.com/coqui-ai/TTS/releases/download/v0.6.1_models/tts_models--it--mai_female--vits.zip
"""
import argparse, glob, hashlib, os, re, subprocess, sys, tempfile

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SCRIPTS = os.path.join(ROOT, "Assets/_Project/Horror/Scripts")
OUT = os.path.join(ROOT, "Assets/_Project/Horror/Resources/Voci")

FIELD_TO_ID = {"Muark": "muark", "Tom": "tom", "Jef": "jef", "Roy": "roy", "Tv": "tv", "Frank": "frank", "Martha": "martha",
               "Gary": "gary", "Linda": "linda", "NeighborMan": "vicino", "NeighborWoman": "vicina"}

# tutti parlano un po' più svelti del modello (che di suo è lento e scandito)
GLOBAL_SPEED = 1.22
# i personaggi umani (ragazzi, vicini, TV) ancora più svelti; quelli del cartone restano così
HUMAN_SPEED = 1.2 * 1.15
CARTOON = {"lucia", "rana", "tasso", "gufo"}
# i personaggi del cartone: un po' più svelti degli umani e poi ancora un 20% (ritmo da cartone animato)
CARTOON_SPEED = 1.2 * 1.2

# voce: modello, tono (1 = invariato), velocità, filtro ffmpeg extra
VOICES = {
    "muark":  ("male",   1.00, 1.00, ""),
    "tom":    ("male",   0.91, 1.00, ""),
    "jef":    ("male",   0.84, 0.88, ""),
    "roy":    ("male",   1.10, 1.08, ""),
    "frank":  ("male",   0.87, 0.90, ""),
    "gary":   ("male",   0.80, 1.00, ""),
    "vicino": ("male",   0.88, 1.00, ""),
    "tv":     ("male",   1.00, 1.03, "highpass=f=320,lowpass=f=3400,acompressor=threshold=0.1:ratio=4,volume=-7dB"),
    "martha": ("female", 0.93, 0.92, ""),
    "linda":  ("female", 1.07, 1.05, ""),
    "vicina": ("female", 1.00, 1.00, ""),
    # i personaggi del cartone "Lucia e il buio" (escono dall'altoparlante della TV)
    "lucia":  ("female", 1.30, 1.05, "highpass=f=250,lowpass=f=4000"),
    "rana":   ("female", 1.18, 1.10, "highpass=f=250,lowpass=f=4000"),
    "tasso":  ("male",   0.86, 0.98, "highpass=f=250,lowpass=f=4000"),
    "gufo":   ("male",   0.80, 0.90, "highpass=f=250,lowpass=f=4000"),
}
IDS = "|".join(VOICES.keys())

STR = r'"((?:[^"\\]|\\.)*)"'

def unescape(s):
    return s.replace('\\"', '"').replace("\\\\", "\\").replace("\\n", "\n")

def key(speaker, text):
    return f"{speaker}_{hashlib.md5(text.encode('utf-8')).hexdigest()[:10]}"

def collect():
    lines = set()
    for path in glob.glob(os.path.join(SCRIPTS, "**/*.cs"), recursive=True):
        src = open(path, encoding="utf-8").read()
        for field, text in re.findall(r"Speakers\.(\w+)\s*,\s*" + STR, src):
            if field in FIELD_TO_ID: lines.add((FIELD_TO_ID[field], unescape(text)))
        for who, text in re.findall(r'"(' + IDS + r')\|((?:[^"\\]|\\.)*)"', src):
            lines.add((who, unescape(text)))
        for body in re.findall(r"\.Choice\(new\[\]\s*\{([^}]*)\}", src):
            for text in re.findall(STR, body): lines.add(("muark", unescape(text)))
    # chiacchiere dei vicini scritte nel generatore della scena
    for path in glob.glob(os.path.join(ROOT, "Assets/_Project/Horror/Editor/*.cs")):
        src = open(path, encoding="utf-8").read()
        for who, text in re.findall(r'"(' + IDS + r')\|((?:[^"\\]|\\.)*)"', src):
            lines.add((who, unescape(text)))
    # battute costruite dal codice (non sono stringhe intere nel sorgente)
    for p in ["Carta! Carta batte sasso.", "Forbice! Forbice batte carta.", "Sasso! Sasso batte forbice."]:
        lines.add(("tom", p + " Ho vinto!"))
    for t in ["Dai...", "Avanti, parti!", "Ti prego, ti prego..."]: lines.add(("muark", t))
    for who in ["tom", "roy", "jef"]:
        for t in ["Ehi, Muark! Dove vai?", "Muark? Ti sto parlando!", "Ehi! Torna qui, dai.", "Ehi, spazio vitale!", "Mi stai addosso, amico.", "...Sì?"]:
            lines.add((who, t))
    return sorted(l for l in lines if re.search(r"[A-Za-zÀ-ÿ]", l[1]))

NUM = {"0": "zero", "1": "uno", "2": "due", "3": "tre", "4": "quattro", "5": "cinque", "6": "sei", "7": "sette", "8": "otto", "9": "nove"}

def for_tts(text):
    t = text
    for a, b in [("à", "a"), ("è", "e"), ("é", "e"), ("ì", "i"), ("ò", "o"), ("ù", "u"), ("È", "E"), ("À", "A"), ("'", "'"), ('"', ""), ("…", "...")]:
        t = t.replace(a, b)
    t = re.sub(r"\d", lambda m: " " + NUM[m.group(0)] + " ", t)
    t = re.sub(r"\.{3,}", ", ", t).strip(" ,")   # i puntini diventano una pausa
    t = re.sub(r"!{2,}", "!", t)
    return t if re.search(r"[A-Za-z]", t) else ""

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--models", required=True)
    ap.add_argument("--force", action="store_true", help="rigenera anche le voci già presenti")
    args = ap.parse_args()
    from TTS.api import TTS
    os.makedirs(OUT, exist_ok=True)
    engines = {}
    def engine(g):
        if g not in engines:
            d = os.path.join(args.models, f"it_{g}", f"tts_models--it--mai_{g}--vits")
            engines[g] = TTS(model_path=os.path.join(d, "model_file.pth"), config_path=os.path.join(d, "config.json"), progress_bar=False)
        return engines[g]

    lines = collect()
    wanted = set()
    made = 0
    for speaker, text in lines:
        k = key(speaker, text)
        wanted.add(k + ".ogg")
        dst = os.path.join(OUT, k + ".ogg")
        if os.path.exists(dst) and not args.force: continue
        spoken = for_tts(text)
        if not spoken: continue
        model, pitch, speed, extra = VOICES.get(speaker, VOICES["vicino"])
        with tempfile.TemporaryDirectory() as tmp:
            wav = os.path.join(tmp, "v.wav")
            engine(model).tts_to_file(text=spoken, file_path=wav)
            # tono: si cambia la frequenza di campionamento, poi si rimette a posto la durata; più un filo di pausa all'inizio
            filters = [f"asetrate={int(16000 * pitch)}", "aresample=22050", "atempo={0:.4f},atempo={0:.4f}".format((speed * GLOBAL_SPEED * (CARTOON_SPEED if speaker in CARTOON else HUMAN_SPEED) / pitch) ** 0.5), "adelay=40", "loudnorm=I=-18:TP=-2"]
            if extra: filters.append(extra)   # dopo la normalizzazione (così il volume della TV resta più basso)
            subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", wav, "-af", ",".join(filters), "-ac", "1", "-c:a", "libvorbis", "-q:a", "4", dst], check=True)
        made += 1
        print(f"[{made}] {speaker}: {text}")
    # via le voci di battute che non esistono più
    for f in os.listdir(OUT):
        if f.endswith(".ogg") and f not in wanted:
            os.remove(os.path.join(OUT, f))
            if os.path.exists(os.path.join(OUT, f + ".meta")): os.remove(os.path.join(OUT, f + ".meta"))
            print("rimossa", f)
    print(f"Fatto: {made} voci nuove, {len(wanted)} battute in tutto.")

if __name__ == "__main__":
    main()
