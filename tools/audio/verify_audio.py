#!/usr/bin/env python3
"""Decode every OGG in Assets/Audio and report duration, size, DC, peak, RMS, edges, loop seams."""
import glob, os, subprocess, numpy as np
def dec(p):
    ch = int(subprocess.check_output(["ffprobe","-v","error","-select_streams","a:0","-show_entries","stream=channels","-of","csv=p=0",p]).strip())
    raw = subprocess.check_output(["ffmpeg","-v","error","-i",p,"-f","f32le","-ac",str(ch),"-ar","44100","-"])
    return np.frombuffer(raw,'<f4').reshape(-1,ch)
for p in sorted(glob.glob("Assets/Audio/**/*.ogg", recursive=True)):
    x = dec(p); m = x.mean(1); loop = "loop" in p
    rms = np.sqrt(np.mean(m**2)); pk = np.max(np.abs(x))
    line = f"{os.path.relpath(p,'Assets/Audio'):32s} {len(x)/44100:6.3f}s {os.path.getsize(p)/1024:6.1f}KB ch={x.shape[1]} dc={m.mean():+.5f} peak={20*np.log10(pk):6.1f}dBFS rms={rms:.3f}"
    if loop:
        seam = np.max(np.abs(x[0]-x[-1])); d = np.abs(np.diff(m)); 
        line += f" seam_jump={seam:.4f} (median step {np.median(d):.4f}, p99 {np.percentile(d,99):.4f})"
    else:
        line += f" first={np.max(np.abs(x[0])):.4f} last={np.max(np.abs(x[-1])):.4f} tail2ms={np.max(np.abs(x[-88:])):.4f}"
    print(line)
