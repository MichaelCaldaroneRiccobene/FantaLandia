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
