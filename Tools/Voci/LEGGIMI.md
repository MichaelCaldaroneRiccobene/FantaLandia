# Voci italiane dei dialoghi

Le battute dei personaggi sono lette da una voce neurale italiana (Coqui TTS, modelli VITS "mai" maschile e femminile).
Ogni battuta è un file `.ogg` in `Assets/_Project/Horror/Resources/Voci`, con nome `<personaggio>_<md5 del testo>`.
Il gioco cerca la voce della battuta; se non la trova (battuta nuova o modificata) usa il borbottio.

## Rigenerare dopo aver cambiato o aggiunto battute

```
pip install torch "coqui-tts[codec]" six
python Tools/Voci/genera_voci.py --models <cartella con it_male/ e it_female/>
```

Serve anche `ffmpeg` nel PATH. I modelli (zip da scompattare in `it_male/` e `it_female/`):
- https://github.com/coqui-ai/TTS/releases/download/v0.6.1_models/tts_models--it--mai_male--vits.zip
- https://github.com/coqui-ai/TTS/releases/download/v0.6.1_models/tts_models--it--mai_female--vits.zip

Lo script genera solo le voci che mancano (`--force` per rifarle tutte) e cancella quelle di battute che non esistono più.
Tono e velocità di ogni personaggio sono nella tabella `VOICES` in cima allo script.

In futuro si possono sostituire i file con registrazioni vere (stesso nome) o con un servizio di sintesi migliore.

## Voci inglesi

Le voci inglesi sono in `Assets/_Project/Horror/Resources/Voci_en` e usano il modello VCTK (109 voci, accenti inglesi e americani):
- https://github.com/coqui-ai/TTS/releases/download/v0.6.1_models/tts_models--en--vctk--vits.zip (scompattato in `<models>/en_vctk`; nel suo
  `config.json` va corretto il percorso di `speakers_file` verso lo `speaker_ids.json` scompattato)
- serve anche `espeak-ng` installato (pronuncia)

```
python Tools/Voci/genera_voci.py --lang en --models <cartella modelli>
```

I testi inglesi vengono dalla tabella `Assets/_Project/Horror/Resources/Lingue/en.txt`, generata da `Tools/Lingue/crea_inglese.py`
con le traduzioni di `Tools/Lingue/traduzioni_en.py`. La voce di ogni personaggio è nella tabella `EN_VOICES` dello script.
