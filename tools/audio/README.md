# Audio tools
- `python3 tools/audio/generate_audio.py` – regenerates every OGG in `Assets/Audio` (deterministic seed). Needs numpy, scipy, ffmpeg with libvorbis. Does not touch `.meta` files.
- `python3 tools/audio/verify_audio.py` – decodes each OGG and prints duration, size, DC, peak, RMS, edge values and loop-seam jumps.
