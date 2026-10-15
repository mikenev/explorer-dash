#!/usr/bin/env python3
"""Procedural audio generator for Explorer Dash (original; no samples).

Run theme "Canopy Scramble": D Dorian, 6/8 at 138 dotted-quarter BPM, 24 bars,
syncopated tribal percussion (log drums, shakers, toms), brass stabs built on a
descending b7-6-5 / 4-3 Dorian riff (no fanfare dotted rhythms, no rising 4ths,
no 4/4 march). Stings: title (Dorian rising cluster swell), game over (Phrygian
bII->i fall). SFX are mono.

Usage: python3 tools/audio/generate_audio.py [out_dir=Assets/Audio]
Requires numpy, scipy, ffmpeg (libvorbis).
"""
import os, sys, subprocess, tempfile
import numpy as np
from scipy import signal

SR = 44100
rng = np.random.default_rng(1234)
OUT = sys.argv[1] if len(sys.argv) > 1 else "Assets/Audio"

def t(d): return np.arange(int(d * SR)) / SR
def env_adsr(n, a, d, s, r):
    a, d, r = int(a*SR), int(d*SR), int(r*SR)
    e = np.full(n, s, float)
    e[:a] = np.linspace(0, 1, a, endpoint=False) if a else e[:a]
    e[a:a+d] = np.linspace(1, s, len(e[a:a+d]))
    if r: e[-r:] *= np.linspace(1, 0, len(e[-r:]))
    return e
def expd(n, tau): return np.exp(-np.arange(n) / (tau * SR))
def lp(x, fc, o=2): return signal.sosfilt(signal.butter(o, min(fc, SR*0.45), 'low', fs=SR, output='sos'), x)
def hp(x, fc, o=2): return signal.sosfilt(signal.butter(o, fc, 'high', fs=SR, output='sos'), x)
def bp(x, lo, hi, o=2): return signal.sosfilt(signal.butter(o, [lo, hi], 'band', fs=SR, output='sos'), x)
def noise(n): return rng.standard_normal(n)
def saw(f, n, det=0.0):
    ph = np.cumsum(np.full(n, f) if np.isscalar(f) else f) / SR
    return 2 * ((ph + det) % 1) - 1
def tvlp(x, cut):
    """time-varying one-pole-ish lowpass via block filtering (filter envelope)."""
    y = np.zeros_like(x); blk = 256; zi = None
    for i in range(0, len(x), blk):
        fc = float(np.clip(cut[i], 40, SR*0.45))
        sos = signal.butter(2, fc, 'low', fs=SR, output='sos')
        if zi is None: zi = np.zeros((sos.shape[0], 2))
        y[i:i+blk], zi = signal.sosfilt(sos, x[i:i+blk], zi=zi)
    return y
def midi(m): return 440 * 2 ** ((m - 69) / 12)

def brass(f, dur, vel=1.0, bright=1.0):
    n = int(dur * SR); vib = 1 + 0.004*np.sin(2*np.pi*5.5*np.arange(n)/SR)
    x = sum(saw(f*vib*(1+d), n, k*0.17) for k, d in enumerate((-0.006, 0, 0.007)))
    x += 0.3*np.sign(np.sin(2*np.pi*np.cumsum(f*vib)/SR))
    e = env_adsr(n, 0.02, 0.12, 0.7, min(0.08, dur*0.4))
    cut = 300 + bright*4500*expd(n, 0.12) + 1200*e
    return tvlp(x, cut) * e * vel * 0.25

def membrane(f0, dur, f1=None, tau=0.15, click=0.3):
    n = int(dur*SR); f1 = f1 or f0*0.6
    f = f1 + (f0-f1)*expd(n, 0.03)
    x = np.sin(2*np.pi*np.cumsum(f)/SR) * expd(n, tau)
    x += click * lp(noise(n), 3000) * expd(n, 0.004)
    return x
def logdrum(f, dur=0.25):
    n = int(dur*SR); x = np.sin(2*np.pi*f*t(dur)) + 0.4*np.sin(2*np.pi*f*2.76*t(dur))
    return x * expd(n, 0.07) + 0.2*bp(noise(n), f, f*4)*expd(n, 0.005)
def shaker(dur=0.06): n = int(dur*SR); return hp(noise(n), 6000) * env_adsr(n, 0.008, 0.01, 0.5, 0.04)*0.3

def reverb_ir(dur=1.4, tau=0.35):
    n = int(dur*SR); ir = noise(n) * expd(n, tau); ir = lp(ir, 5000); ir[0] = 0
    return ir / np.sqrt(np.sum(ir**2)) * 0.5
def reverb(x, wet=0.25, dur=1.4, tau=0.35, circular=False):
    ir = reverb_ir(dur, tau)
    if circular:
        L = len(x); y = np.real(np.fft.ifft(np.fft.fft(x) * np.fft.fft(ir, L)))
        return x*(1-wet) + y*wet*2
    y = signal.fftconvolve(x, ir)
    out = np.zeros(len(y)); out[:len(x)] = x*(1-wet); out += y*wet*2
    return out

def add(buf, x, pos, gain=1.0, wrap=False):
    i = int(pos*SR)
    if wrap:
        idx = (i + np.arange(len(x))) % len(buf); np.add.at(buf, idx, x*gain)
    else:
        end = min(len(buf), i+len(x)); buf[i:end] += x[:end-i]*gain

def fade(x, fin=0.003, fout=0.03):
    a, b = int(fin*SR), int(fout*SR)
    x = x.copy()
    if a: x[:a] *= np.linspace(0, 1, a)
    if b: x[-b:] *= np.linspace(1, 0, b) ** 2
    x[0] = 0; x[-1] = 0
    return x
def dc(x):
    if x.ndim == 1: return x - x.mean()
    return x - x.mean(axis=0)
def peak_norm(x, db=-1.0): return x / np.max(np.abs(x)) * 10**(db/20)
def rms_norm(x, target=0.12, ceil_db=-1.5):
    r = np.sqrt(np.mean(x**2)); x = x * target / r
    pk = np.max(np.abs(x)); c = 10**(ceil_db/20)
    if pk > c: x = np.tanh(x / c * 0.9) / np.tanh(0.9) * c  # soft limit
    return x
def trim_lead(x, thr=1e-4):
    i = np.argmax(np.abs(x) > thr); return x[i:]

# ---------------------------------------------------------------- music
BPM = 138                     # dotted-quarter pulse
E8 = 60 / BPM / 3             # eighth note
BAR = 6 * E8
def music_loop():
    bars = 24; L = int(round(bars*BAR*SR)); L -= L % 2
    Lb = np.zeros(L); Rb = np.zeros(L)
    def put(x, pos, g=1, pan=0.0):
        add(Lb, x, pos, g*np.sqrt((1-pan)/2)*1.414, wrap=True)
        add(Rb, x, pos, g*np.sqrt((1+pan)/2)*1.414, wrap=True)
    # chord roots per 2 bars (D Dorian: i  bVII  IV  i | i bVII v IV ...)
    prog = [50, 48, 55, 50, 50, 48, 57, 55, 53, 48, 55, 50]   # D C G D D C A G F C G D
    for b in range(bars):
        s = b*BAR
        # percussion: 6/8 tribal: low tom on 1 and the 'and' of 4 (syncopated)
        put(membrane(90, 0.35, 55), s, 0.9)
        put(membrane(90, 0.35, 55), s + 3.5*E8 if b % 2 else s + 4*E8, 0.7)
        put(membrane(170, 0.2, 120, 0.08), s + 2*E8, 0.45, -0.4)
        put(membrane(150, 0.2, 110, 0.08), s + 5*E8, 0.4, 0.4)
        for k in range(6):
            put(shaker(), s + k*E8 + (0.012 if k % 2 else 0), 0.7 if k in (0, 3) else 0.45, 0.6)
        # log-drum ostinato 1 . 3 4 . 6  -> (0,2,3,5) in Dorian
        for k, m in zip((0, 2, 3, 5), (62, 69, 65, 64) if b % 2 == 0 else (62, 67, 65, 60)):
            put(logdrum(midi(m)), s + k*E8, 0.25, -0.5)
        if b % 4 == 3:  # fill
            for k in range(6): put(membrane(200-15*k, 0.15, 130-8*k, 0.06), s + k*E8, 0.35, (k-2.5)/3)
    for i, r in enumerate(prog):
        s = i*2*BAR
        # bass: dotted-quarter pumping with off-beat pickups
        for k, off in ((0, 0), (3, 0), (5, 7)):
            n = brass(midi(r - 12 + off), E8*(2.6 if k != 5 else 0.9), 0.9, 0.3)
            put(lp(n, 900), s + k*E8, 1.0); put(lp(n, 900), s + BAR + k*E8, 1.0)
        # brass stab chords on syncopated eighths (2 and 5)
        chord = [r, r+3 if r in (50, 55, 57) else r+4, r+7]
        for bb in (0, 1):
            for k in (1, 4) if bb == 0 else (1, 3.5):
                for j, m in enumerate(chord):
                    put(brass(midi(m+12), E8*0.8, 0.35, 0.8), s + bb*BAR + k*E8, 1, (j-1)*0.5)
    # lead riff: descending Dorian figure (C B A | G F E D), never a rising 4th leap
    riffA = [(0, 72, 1), (1, 71, 1), (2, 69, 2), (4, 67, 1), (5, 69, 1),
             (6, 65, 2), (8, 64, 1), (9, 62, 3)]
    riffB = [(0, 69, 1), (1, 71, 1), (2, 72, 1), (3, 74, 2), (5, 72, 1),
             (6, 71, 1), (7, 69, 2), (9, 67, 1), (10, 65, 2)]
    for phrase in range(bars // 2):
        if phrase % 6 in (0, 1): continue   # breathe: percussion+stabs only
        riff = riffA if phrase % 2 == 0 else riffB
        s = phrase*2*BAR
        for k, m, d in riff:
            put(brass(midi(m), E8*d*0.95, 0.6, 1.2), s + k*E8, 1, 0.15)
            put(brass(midi(m-12), E8*d*0.95, 0.3, 0.7), s + k*E8, 1, -0.15)
    st = np.stack([Lb, Rb], 1)
    st = np.stack([reverb(st[:, 0], 0.18, circular=True), reverb(st[:, 1], 0.18, circular=True)], 1)
    st = dc(st)
    return st / np.max(np.abs(st)) * 10**(-1.5/20)

def sting(kind):
    if kind == "title":
        dur = 3.6; L = int(dur*SR); x = np.zeros(L)
        # D Dorian cluster rising stepwise: D E F G A B(nat6!) -> D maj-add9 open
        for i, m in enumerate((50, 52, 53, 55, 57, 59)):
            add(x, brass(midi(m+12), 0.18, 0.5, 1.2), i*0.11)
        for m in (38, 50, 57, 62, 64, 69):
            add(x, brass(midi(m), 2.2, 0.45, 1.0), 0.7)
        for i in range(6): add(x, membrane(160-15*i, 0.2, 100, 0.07), i*0.11, 0.5)
        add(x, membrane(70, 1.2, 40, 0.5), 0.7, 1.0)
        add(x, lp(noise(int(1.5*SR)), 2000)*expd(int(1.5*SR), 0.4), 0.7, 0.15)
    else:
        dur = 3.4; L = int(dur*SR); x = np.zeros(L)
        # Phrygian fall: Eb major (bII) -> D minor, sagging pitch
        for m in (51, 55, 58): add(x, brass(midi(m+12), 0.5, 0.5, 1.0), 0.0)
        for m in (50, 53, 57, 38): 
            n = int(1.8*SR); f = midi(m+12) * (1 - 0.06*(1-expd(n, 0.6)))
            add(x, tvlp(saw(f, n) + saw(f*1.005, n, .3), 300+2500*expd(n, 0.3)) * env_adsr(n, 0.02, 0.2, 0.6, 0.6)*0.25, 0.55)
        for p, f in ((0.0, 120), (0.55, 80)): add(x, membrane(f, 0.6, 45, 0.3), p, 1.0)
    st = reverb(x, 0.3, 2.0, 0.5)
    st = trim_lead(st)
    st = fade(dc(st), 0.002, 0.6)
    st = peak_norm(st, -1.5)
    return np.stack([st, st*0.98 + 0.02*np.roll(st, 400)], 1)

# ---------------------------------------------------------------- sfx
def sfx_footstep(v):
    d = 0.16; n = int(d*SR); g = rng.uniform(0.85, 1.15)
    x = membrane(70*g, d, 45*g, 0.03, 0.8) + 0.5*bp(noise(n), 300*g, 2500*g)*expd(n, 0.02)
    x += 0.15*hp(noise(n), 4000)*expd(n, 0.04) * (v % 2)  # leaf crunch on alternates
    return x
def sfx_jump():
    d = 0.32; n = int(d*SR); f = 220 + 500*(1-expd(n, 0.08))
    x = 0.5*bp(noise(n), 800, 4000) * env_adsr(n, 0.01, 0.1, 0.3, 0.15)
    x += 0.4*np.sin(2*np.pi*np.cumsum(f)/SR) * expd(n, 0.08)
    return x + 0.6*sfx_footstep(0)[:n] if n <= int(0.16*SR) else x + np.pad(sfx_footstep(0), (0, n - int(0.16*SR)))*0.6
def sfx_slide():
    d = 0.7; n = int(d*SR); e = env_adsr(n, 0.03, 0.1, 0.8, 0.3)
    return bp(noise(n), 400, 3000) * e * (1 + 0.3*np.sin(2*np.pi*13*t(d)))
def sfx_stumble():
    x = np.zeros(int(0.55*SR))
    for p, g in ((0, 1.0), (0.09, 0.7), (0.21, 0.9)): add(x, sfx_footstep(1), p, g)
    add(x, membrane(110, 0.25, 70, 0.08, 0.5), 0.3, 0.8)  # "oof"-ish thud
    return x
def sfx_boulder_loop():
    d = 2.0; L = int(d*SR)
    # circular: build in frequency domain-friendly way; low rumble + periodic tumble (2 Hz) + grit
    tt = np.arange(L)/SR
    base = noise(L); base = np.real(np.fft.ifft(np.fft.fft(base) * (np.abs(np.fft.fftfreq(L, 1/SR)) < 120)))
    mod = 0.7 + 0.3*np.sin(2*np.pi*2*tt)  # 4 cycles per loop
    grit = np.real(np.fft.ifft(np.fft.fft(noise(L)) * ((np.abs(np.fft.fftfreq(L, 1/SR)) > 800) & (np.abs(np.fft.fftfreq(L, 1/SR)) < 3000))))
    x = base/np.std(base)*mod + 0.15*grit/np.std(grit)*(0.5+0.5*np.sin(2*np.pi*4*tt)**2)
    for k in range(4): add(x, membrane(45, 0.4, 30, 0.15, 0.2), k*0.5 + 0.07, 1.2, wrap=True)
    return x
def sfx_dart():
    x = np.zeros(int(0.45*SR)); n = int(0.25*SR)
    w = bp(noise(n), 1500, 7000) * np.sin(np.pi*np.arange(n)/n)**2
    add(x, w, 0, 0.8)
    m = int(0.08*SR); click = bp(noise(m), 2000, 6000)*expd(m, 0.004) + 0.6*np.sin(2*np.pi*1800*t(0.08))*expd(m, 0.01)
    add(x, click, 0.25, 1.2)
    return x
def sfx_gator():
    x = np.zeros(int(0.9*SR)); n = int(0.55*SR)
    add(x, hp(noise(n), 2500)*env_adsr(n, 0.08, 0.1, 0.7, 0.2)*(1+0.4*np.sin(2*np.pi*30*t(0.55))), 0, 0.6)
    m = int(0.2*SR); add(x, membrane(140, 0.2, 60, 0.04, 1.2) + bp(noise(m), 500, 3000)*expd(m, 0.01), 0.6, 1.0)
    return x
def sfx_vine():
    d = 0.42; n = int(d*SR); fc = 600 + 3500*np.sin(np.pi*np.arange(n)/n)
    return tvlp(noise(n), fc) * np.sin(np.pi*np.arange(n)/n)**1.5
def sfx_pit():
    d = 1.4; n = int(d*SR); f = 900*expd(n, 0.5) + 120
    x = 0.4*np.sin(2*np.pi*np.cumsum(f)/SR)*env_adsr(n, 0.01, 0.2, 0.6, 0.4)
    x += 0.3*tvlp(noise(n), 3000*expd(n, 0.5)+200)*env_adsr(n, 0.05, 0.1, 0.8, 0.3)
    x = np.pad(x, (0, int(0.4*SR))); add(x, membrane(60, 0.4, 35, 0.12, 0.6), 1.3, 0.8)
    return x
def sfx_crushed():
    d = 1.0; n = int(d*SR); x = membrane(55, d, 30, 0.25, 1.0)
    x += 0.6*lp(noise(n), 1500)*expd(n, 0.12)
    for k in range(5): add(x, bp(noise(int(0.05*SR)), 1500, 5000)*expd(int(0.05*SR), 0.01), 0.03 + k*0.045*rng.uniform(0.6, 1.4), 0.5)
    return x
def sfx_ui_click():
    m = int(0.06*SR); return logdrum(midi(81), 0.06)[:m] * 0.8 + 0.2*bp(noise(m), 3000, 8000)*expd(m, 0.002)

# ---------------------------------------------------------------- write
def write_ogg(path, x, q):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    x = np.clip(x, -1, 1); ch = 1 if x.ndim == 1 else x.shape[1]
    with tempfile.NamedTemporaryFile(suffix=".f32", delete=False) as f:
        f.write(x.astype('<f4').tobytes()); raw = f.name
    subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-f", "f32le", "-ar", str(SR), "-ac", str(ch),
                    "-i", raw, "-c:a", "libvorbis", "-q:a", str(q), path], check=True)
    os.unlink(raw)

def main():
    write_ogg(f"{OUT}/Music/run_theme_loop.ogg", music_loop(), 4)
    write_ogg(f"{OUT}/Music/title_sting.ogg", sting("title"), 4)
    write_ogg(f"{OUT}/Music/gameover_sting.ogg", sting("over"), 4)
    one_shots = {f"footstep_{i+1:02d}": (lambda i=i: sfx_footstep(i)) for i in range(4)}
    one_shots.update(jump=sfx_jump, slide=sfx_slide, stumble=sfx_stumble, dart_whoosh_click=sfx_dart,
                     gator_hiss_snap=sfx_gator, vine_swish=sfx_vine, pit_fall=sfx_pit, crushed=sfx_crushed,
                     ui_click=sfx_ui_click)
    quiet = {"ui_click": 0.07, **{f"footstep_{i+1:02d}": 0.09 for i in range(4)}}
    for name, fn in one_shots.items():
        x = trim_lead(dc(fn()), 1e-3)
        x = fade(dc(x), 0.002, 0.03 if name != "pit_fall" else 0.15)
        x = fade(rms_norm(x, quiet.get(name, 0.12)), 0.002, 0.03)
        write_ogg(f"{OUT}/SFX/{name}.ogg", x, 3)
    b = dc(sfx_boulder_loop()); b = rms_norm(b, 0.12)
    b = b - b.mean()
    write_ogg(f"{OUT}/SFX/boulder_rumble_loop.ogg", b, 3)

if __name__ == "__main__":
    main()
