"""
Generate high-fidelity arcade sound effects for Turbo Circuit.
Replaces muffled/low-frequency placeholder tones with crisp, punchy,
and bright arcade sounds:
- Countdown beep (sfx_beep.ogg)
- Go burst (sfx_go.ogg)
- Lap chime (sfx_lap.ogg)
- Final lap fanfare (sfx_finallap.ogg)
- Finish victory fanfare (sfx_finish.ogg)
- Drift charge spark / pop (sfx_pop.ogg)
- Item roulette tick (sfx_tick.ogg)
- Item locked-in jingle (sfx_gotitem.ogg)

Usage:
    uv run --with soundfile --with numpy python Tools/Audio/generate_race_sfx.py
"""

import os
import numpy as np
import soundfile as sf

SR = 44100

def envelope(n_samples, a_len, d_len, s_lvl, r_len):
    """Generates an ADSR envelope."""
    a = int(a_len * SR)
    d = int(d_len * SR)
    r = int(r_len * SR)
    s = max(0, n_samples - a - d - r)
    
    env_a = np.linspace(0, 1, a, endpoint=False) if a > 0 else np.array([])
    env_d = np.linspace(1, s_lvl, d, endpoint=False) if d > 0 else np.array([])
    env_s = np.full(s, s_lvl) if s > 0 else np.array([])
    env_r = np.linspace(s_lvl, 0, r) if r > 0 else np.array([])
    
    env = np.concatenate([env_a, env_d, env_s, env_r])
    if len(env) < n_samples:
        env = np.pad(env, (0, n_samples - len(env)))
    return env[:n_samples]

def fm_bell(freq, dur, mod_ratio=2.0, mod_depth=1.8, attack=0.002, decay_mod=8.0, decay_car=4.0):
    """
    FM synthesis bell / arcade chime:
    Modulator generates bright metallic harmonics at onset, decaying into a pure clear tone.
    """
    n = int(dur * SR)
    t = np.linspace(0, dur, n, endpoint=False)
    
    # Modulator envelope decays fast for crisp attack
    env_mod = np.exp(-t * decay_mod)
    mod = np.sin(2 * np.pi * freq * mod_ratio * t) * (mod_depth * env_mod)
    
    # Carrier envelope
    env_car = np.exp(-t * decay_car)
    ramp_n = int(attack * SR)
    if ramp_n > 0:
        env_car[:ramp_n] *= np.linspace(0, 1, ramp_n)
        
    carrier = np.sin(2 * np.pi * freq * t + mod) * env_car
    
    # Add subtle octave overtone for crystalline brilliance
    overtone = np.sin(2 * np.pi * freq * 2.0 * t) * np.exp(-t * (decay_car * 1.5)) * 0.25
    
    return carrier + overtone

def synth_brass(freq, dur, attack=0.02, decay=0.1, sustain=0.65, release=0.08):
    """
    Bright synth brass with multiple detuned saws and resonant filter opening.
    """
    n = int(dur * SR)
    t = np.linspace(0, dur, n, endpoint=False)
    
    f1 = freq * 0.998
    f2 = freq * 1.002
    saw1 = 2.0 * (t * f1 - np.floor(t * f1 + 0.5))
    saw2 = 2.0 * (t * f2 - np.floor(t * f2 + 0.5))
    raw = (saw1 + saw2) * 0.5
    
    fund = np.sin(2 * np.pi * freq * t) * 0.4
    harmonic2 = np.sin(2 * np.pi * freq * 2.0 * t) * 0.2
    
    env = envelope(n, attack, decay, sustain, release)
    sig = (raw * 0.6 + fund + harmonic2) * env
    return sig

def add_sparkle(sig, freq_sparkle, dur, delay=0.0):
    """Adds high-register chime sparkle."""
    n = len(sig)
    t = np.linspace(0, dur, n, endpoint=False)
    sparkle = np.sin(2 * np.pi * freq_sparkle * t) * np.exp(-np.maximum(0, t - delay) * 12.0)
    sparkle[t < delay] = 0.0
    return sig + sparkle * 0.2

def add_stereo_delay(sig, delay_ms=45, feedback=0.28, spread=0.6):
    """Expands mono signal into wide, polished stereo arcade sound."""
    d_samples_l = int((delay_ms * 0.8) * SR / 1000.0)
    d_samples_r = int((delay_ms * 1.2) * SR / 1000.0)
    
    out_len = len(sig) + max(d_samples_l, d_samples_r) * 2
    l = np.zeros(out_len)
    r = np.zeros(out_len)
    
    l[:len(sig)] += sig * (1.0 - spread * 0.3)
    r[:len(sig)] += sig * (1.0 - spread * 0.3)
    
    if d_samples_l < out_len:
        echo_l = np.pad(sig * feedback, (d_samples_l, out_len - len(sig) - d_samples_l))[:out_len]
        l += echo_l * (1.0 + spread * 0.5)
        r += echo_l * (1.0 - spread * 0.5)
        
    if d_samples_r < out_len:
        echo_r = np.pad(sig * (feedback * feedback), (d_samples_r, out_len - len(sig) - d_samples_r))[:out_len]
        l += echo_r * (1.0 - spread * 0.5)
        r += echo_r * (1.0 + spread * 0.5)
        
    return np.vstack([l, r]).T

def normalize_stereo(stereo_data, target_peak=0.92):
    peak = np.max(np.abs(stereo_data))
    if peak > 1e-5:
        return stereo_data * (target_peak / peak)
    return stereo_data

# ==========================================
# 1. Beep (3, 2, 1 Countdown)
# ==========================================
def make_countdown_beep():
    dur = 0.26
    sig = fm_bell(1046.5, dur, mod_ratio=2.0, mod_depth=1.6, attack=0.001, decay_mod=16.0, decay_car=9.0)
    sig = add_sparkle(sig, 2093.0, dur, delay=0.0)
    stereo = add_stereo_delay(sig, delay_ms=30, feedback=0.20, spread=0.4)
    return normalize_stereo(stereo, target_peak=0.92)

# ==========================================
# 2. Go! (Race Start)
# ==========================================
def make_countdown_go():
    dur = 0.75
    n = int(dur * SR)
    t = np.linspace(0, dur, n, endpoint=False)
    
    sweep_dur = 0.045
    n_sw = int(sweep_dur * SR)
    t_sw = t[:n_sw]
    freq_sweep = np.linspace(523.25, 2093.0, n_sw)
    phase_sweep = 2 * np.pi * np.cumsum(freq_sweep) / SR
    zap = np.sin(phase_sweep) * np.linspace(0.8, 0.2, n_sw)
    zap_full = np.pad(zap, (0, n - n_sw))
    
    c6 = fm_bell(1046.5, dur, mod_ratio=2.0, mod_depth=1.4, attack=0.002, decay_mod=10.0, decay_car=4.5)
    e6 = fm_bell(1318.5, dur, mod_ratio=2.0, mod_depth=1.2, attack=0.003, decay_mod=11.0, decay_car=5.0)
    g6 = fm_bell(1567.98, dur, mod_ratio=2.0, mod_depth=1.0, attack=0.004, decay_mod=12.0, decay_car=5.5)
    c7 = fm_bell(2093.0, dur, mod_ratio=2.0, mod_depth=0.8, attack=0.002, decay_mod=14.0, decay_car=6.0)
    
    chord = (c6 * 0.4 + e6 * 0.3 + g6 * 0.35 + c7 * 0.35)
    combined = zap_full * 0.35 + chord
    stereo = add_stereo_delay(combined, delay_ms=50, feedback=0.32, spread=0.7)
    return normalize_stereo(stereo, target_peak=0.93)

# ==========================================
# 3. Lap (Lap Passing)
# ==========================================
def make_lap():
    dur = 0.55
    n = int(dur * SR)
    sig = np.zeros(n)
    
    notes = [(783.99, 0.00, 0.35), (1046.50, 0.07, 0.38), (1318.51, 0.14, 0.40)]
    for freq, start_t, note_dur in notes:
        start_idx = int(start_t * SR)
        note_sig = fm_bell(freq, note_dur, mod_ratio=2.0, mod_depth=1.3, attack=0.002, decay_mod=12.0, decay_car=6.5)
        sig[start_idx:start_idx + len(note_sig)] += note_sig * 0.45
        
    stereo = add_stereo_delay(sig, delay_ms=40, feedback=0.25, spread=0.6)
    return normalize_stereo(stereo, target_peak=0.92)

# ==========================================
# 4. Final Lap (Final Lap Warning)
# ==========================================
def make_final_lap():
    dur = 1.25
    n = int(dur * SR)
    sig = np.zeros(n)
    
    pattern = [
        (523.25, 0.00, 0.10, 0.45),
        (659.25, 0.09, 0.10, 0.45),
        (783.99, 0.18, 0.10, 0.50),
        (1046.50, 0.27, 0.16, 0.60),
        (1567.98, 0.42, 0.70, 0.75),
    ]
    
    for freq, start_t, note_dur, vol in pattern:
        start_idx = int(start_t * SR)
        brass = synth_brass(freq, note_dur, attack=0.005, decay=0.06, sustain=0.6, release=0.08)
        bell = fm_bell(freq, note_dur, mod_ratio=2.0, mod_depth=1.2, attack=0.002, decay_mod=10.0, decay_car=4.0)
        note_sig = (brass * 0.6 + bell * 0.4) * vol
        end_idx = min(n, start_idx + len(note_sig))
        sig[start_idx:end_idx] += note_sig[:end_idx - start_idx]
        
    stereo = add_stereo_delay(sig, delay_ms=55, feedback=0.35, spread=0.75)
    return normalize_stereo(stereo, target_peak=0.94)

# ==========================================
# 5. Finish (Race Complete Fanfare)
# ==========================================
def make_finish():
    dur = 2.1
    n = int(dur * SR)
    sig = np.zeros(n)
    
    motif = [
        (523.25, 0.00, 0.10, 0.45),
        (659.25, 0.09, 0.10, 0.45),
        (783.99, 0.18, 0.14, 0.50),
        (880.00, 0.32, 0.10, 0.50),
        (783.99, 0.41, 0.10, 0.50),
        (1046.50, 0.50, 0.20, 0.60),
        (523.25, 0.70, 1.25, 0.30),
        (783.99, 0.70, 1.25, 0.35),
        (1046.50, 0.70, 1.25, 0.45),
        (1318.51, 0.70, 1.25, 0.45),
        (1567.98, 0.70, 1.25, 0.50),
        (2093.00, 0.70, 1.25, 0.35),
    ]
    
    for freq, start_t, note_dur, vol in motif:
        start_idx = int(start_t * SR)
        brass = synth_brass(freq, note_dur, attack=0.006, decay=0.08, sustain=0.65, release=0.15)
        bell = fm_bell(freq, note_dur, mod_ratio=2.0, mod_depth=1.3, attack=0.002, decay_mod=8.0, decay_car=3.0)
        note_sig = (brass * 0.6 + bell * 0.4) * vol
        end_idx = min(n, start_idx + len(note_sig))
        sig[start_idx:end_idx] += note_sig[:end_idx - start_idx]
        
    stereo = add_stereo_delay(sig, delay_ms=65, feedback=0.40, spread=0.8)
    return normalize_stereo(stereo, target_peak=0.94)

# ==========================================
# 6. Pop / Drift Charge Spark (sfx_pop.ogg)
# Crisp, bright crystal spark when drift charges level 1, 2, or 3
# ==========================================
def make_drift_spark():
    dur = 0.18
    n = int(dur * SR)
    t = np.linspace(0, dur, n, endpoint=False)
    
    # 1) Tiny high-frequency noise burst for realistic ignition crackle (0 - 5ms)
    noise_len = int(0.006 * SR)
    noise = np.random.uniform(-1, 1, noise_len) * np.linspace(1, 0, noise_len)**2
    noise_full = np.pad(noise, (0, n - noise_len))
    
    # 2) Brilliant FM crystal spark at 1318.5Hz (E6) with fast decay
    spark = fm_bell(1318.5, dur, mod_ratio=3.5, mod_depth=2.2, attack=0.001, decay_mod=32.0, decay_car=18.0)
    overtone = np.sin(2 * np.pi * 2637.0 * t) * np.exp(-t * 24.0) * 0.3
    
    sig = noise_full * 0.3 + spark * 0.7 + overtone
    stereo = add_stereo_delay(sig, delay_ms=20, feedback=0.15, spread=0.4)
    return normalize_stereo(stereo, target_peak=0.92)

# ==========================================
# 7. Roulette Tick (sfx_tick.ogg)
# Crisp, light mechanical clicker (2800Hz) for rapid spinning roulette
# ==========================================
def make_roulette_tick():
    dur = 0.045
    n = int(dur * SR)
    t = np.linspace(0, dur, n, endpoint=False)
    
    # Micro click pulse: high resonant chime + impulse
    click = np.sin(2 * np.pi * 2800.0 * t) * np.exp(-t * 110.0)
    click += np.sin(2 * np.pi * 4200.0 * t) * np.exp(-t * 140.0) * 0.5
    impulse = np.exp(-t * 250.0) * 0.4
    
    sig = click + impulse
    stereo = np.vstack([sig, sig * 0.95]).T
    return normalize_stereo(stereo, target_peak=0.90)

# ==========================================
# 8. Got Item (sfx_gotitem.ogg)
# Celebratory 2-note pop burst + C-major chime when item locks in
# ==========================================
def make_got_item():
    dur = 0.42
    n = int(dur * SR)
    sig = np.zeros(n)
    
    # 2 rapid ascending notes into chord: G5 (784Hz) -> C6 (1046.5Hz) with sparkle
    notes = [
        (783.99, 0.00, 0.18, 0.4),
        (1046.50, 0.06, 0.34, 0.6),
        (1318.51, 0.07, 0.33, 0.4),
        (1567.98, 0.08, 0.32, 0.4),
        (2093.00, 0.09, 0.30, 0.3),
    ]
    
    for freq, start_t, note_dur, vol in notes:
        start_idx = int(start_t * SR)
        bell = fm_bell(freq, note_dur, mod_ratio=2.0, mod_depth=1.4, attack=0.001, decay_mod=14.0, decay_car=7.0)
        end_idx = min(n, start_idx + len(bell))
        sig[start_idx:end_idx] += bell[:end_idx - start_idx] * vol
        
    stereo = add_stereo_delay(sig, delay_ms=35, feedback=0.25, spread=0.6)
    return normalize_stereo(stereo, target_peak=0.93)

def main():
    target_dir = os.path.abspath("Assets/Resources/Audio")
    os.makedirs(target_dir, exist_ok=True)
    
    generators = {
        "sfx_beep.ogg": make_countdown_beep,
        "sfx_go.ogg": make_countdown_go,
        "sfx_lap.ogg": make_lap,
        "sfx_finallap.ogg": make_final_lap,
        "sfx_finish.ogg": make_finish,
        "sfx_pop.ogg": make_drift_spark,
        "sfx_tick.ogg": make_roulette_tick,
        "sfx_gotitem.ogg": make_got_item,
    }
    
    for filename, gen_fn in generators.items():
        out_path = os.path.join(target_dir, filename)
        data = gen_fn()
        sf.write(out_path, data, SR, format='OGG', subtype='VORBIS')
        peak = np.max(np.abs(data))
        print(f"Generated {filename:16s}: {len(data)/SR:.2f}s, ch={data.shape[1]}, peak={peak:.2f}")

if __name__ == "__main__":
    main()
