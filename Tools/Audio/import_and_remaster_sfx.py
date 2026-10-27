"""
Import and remaster high-quality CC0 sound effects for Turbo Circuit.
Fetches distributed audio assets from official Kenney repositories and OpenGameArt,
normalizes dynamic range, applies studio envelopes/stereo widening, and outputs
clean OGG files into Assets/Resources/Audio/.

Sources:
- OpenGameArt: 'Race Start Countdown' by kheetor (CC0)
- Kenney: Sci-Fi Sounds, Impact Sounds, Interface Sounds (CC0)

Usage:
    uv run --with soundfile --with numpy python Tools/Audio/import_and_remaster_sfx.py
"""

import io
import os
import urllib.request
import numpy as np
import soundfile as sf

TARGET_DIR = os.path.join(os.path.dirname(__file__), "..", "..", "Assets", "Resources", "Audio")
SR = 44100

def fetch(url):
    print(f"Downloading: {url} ...")
    req = urllib.request.Request(url, headers={"User-Agent": "Mozilla/5.0"})
    with urllib.request.urlopen(req) as resp:
        data = resp.read()
    audio, sr = sf.read(io.BytesIO(data))
    return audio, sr

def to_stereo(audio):
    if audio.ndim == 1:
        return np.column_stack([audio, audio])
    if audio.shape[1] == 1:
        return np.column_stack([audio[:, 0], audio[:, 0]])
    return audio

def widen_stereo(audio, delay_ms=8.0):
    """Subtle Haas stereo widening for punchy mono sounds."""
    audio = to_stereo(audio)
    delay_samples = int(delay_ms * SR / 1000.0)
    out = np.copy(audio)
    out[delay_samples:, 1] = 0.85 * audio[:-delay_samples, 1] + 0.15 * audio[delay_samples:, 1]
    return out

def apply_fade(audio, in_s=0.005, out_s=0.03):
    in_n = min(len(audio), int(in_s * SR))
    out_n = min(len(audio), int(out_s * SR))
    if in_n > 0:
        fade_in = np.linspace(0.0, 1.0, in_n)[:, None] if audio.ndim > 1 else np.linspace(0.0, 1.0, in_n)
        audio[:in_n] *= fade_in
    if out_n > 0:
        fade_out = np.linspace(1.0, 0.0, out_n)[:, None] if audio.ndim > 1 else np.linspace(1.0, 0.0, out_n)
        audio[-out_n:] *= fade_out
    return audio

def normalize(audio, target_peak=0.89):
    peak = np.max(np.abs(audio))
    if peak > 1e-5:
        return audio * (target_peak / peak)
    return audio

def save_ogg(audio, filename, target_peak=0.89):
    audio = to_stereo(audio)
    audio = normalize(audio, target_peak)
    path = os.path.join(TARGET_DIR, filename)
    sf.write(path, audio, SR, format="OGG")
    dur = len(audio) / SR
    print(f"Saved: {filename:18} | dur: {dur:.3f}s | peak: {np.max(np.abs(audio)):.3f}")

def main():
    os.makedirs(TARGET_DIR, exist_ok=True)
    
    # 1. Countdown Beep & GO from OpenGameArt (kheetor, CC0)
    c_audio, c_sr = fetch("https://opengameart.org/sites/default/files/countdown.ogg")
    c_audio = to_stereo(c_audio)
    # Beep (first prompt, 0.012s to 0.48s)
    beep = c_audio[int(0.012 * c_sr):int(0.48 * c_sr)].copy()
    beep = apply_fade(beep, in_s=0.003, out_s=0.04)
    save_ogg(beep, "sfx_beep.ogg", target_peak=0.89)
    
    # GO (high octave burst at 3.01s to 4.22s)
    go = c_audio[int(3.01 * c_sr):int(4.22 * c_sr)].copy()
    go = apply_fade(go, in_s=0.004, out_s=0.06)
    save_ogg(go, "sfx_go.ogg", target_peak=0.90)

    # 2. Kenney Sci-Fi Sounds (CC0)
    scifi_base = "https://raw.githubusercontent.com/Boyquotes/kenney-sci-fi-sounds-for-godot/master/addons/kenney%20sci-fi%20sounds/"
    
    # Boost (Rocket Thruster roar)
    thruster, _ = fetch(scifi_base + "thruster_fire_000.ogg")
    thruster_slice = thruster[:int(1.1 * SR)].copy()
    t = np.linspace(0, 1.1, len(thruster_slice))
    env = np.exp(-t * 2.2) * 0.75 + 0.25 * (1.0 - t / 1.1)
    thruster_slice *= env
    thruster_stereo = widen_stereo(thruster_slice, delay_ms=10.0)
    thruster_stereo = apply_fade(thruster_stereo, in_s=0.005, out_s=0.08)
    save_ogg(thruster_stereo, "sfx_boost.ogg", target_peak=0.89)

    # Missile (Supersonic laser/rocket launch)
    laser, _ = fetch(scifi_base + "laser_large_000.ogg")
    laser_stereo = widen_stereo(laser, delay_ms=6.0)
    laser_stereo = apply_fade(laser_stereo, in_s=0.002, out_s=0.04)
    save_ogg(laser_stereo, "sfx_missile.ogg", target_peak=0.89)

    # Explosion (Crunchy sub-bass explosion)
    exp_crunch, _ = fetch(scifi_base + "explosion_crunch_000.ogg")
    exp_low, _ = fetch(scifi_base + "low_frequency_explosion_001.ogg")
    exp_crunch = to_stereo(exp_crunch)
    exp_low = to_stereo(exp_low)
    min_len = min(len(exp_crunch), len(exp_low))
    exp_blend = exp_crunch[:min_len] * 0.70 + exp_low[:min_len] * 0.60
    exp_blend = apply_fade(exp_blend, in_s=0.002, out_s=0.05)
    save_ogg(exp_blend, "sfx_explosion.ogg", target_peak=0.90)

    # Drop (Slime / Banana drop)
    slime, _ = fetch(scifi_base + "slime_000.ogg")
    slime_stereo = widen_stereo(slime, delay_ms=5.0)
    slime_stereo = apply_fade(slime_stereo, in_s=0.002, out_s=0.04)
    save_ogg(slime_stereo, "sfx_drop.ogg", target_peak=0.89)

    # Shield / Star (Invincible force field aura)
    force, _ = fetch(scifi_base + "force_field_000.ogg")
    force_stereo = widen_stereo(force, delay_ms=8.0)
    force_stereo = apply_fade(force_stereo, in_s=0.005, out_s=0.05)
    save_ogg(force_stereo, "sfx_shield.ogg", target_peak=0.89)
    save_ogg(force_stereo, "sfx_star.ogg", target_peak=0.89)

    # 3. Kenney Impact Sounds (CC0)
    impact_base = "https://raw.githubusercontent.com/ETdoFresh/kenney.nl/master/kenney_impactsounds/Audio/"
    
    # Bumps (Chassis / Guardrail collision variants)
    b0, _ = fetch(impact_base + "impactMetal_heavy_000.ogg")
    save_ogg(apply_fade(to_stereo(b0), in_s=0.001, out_s=0.02), "sfx_bump_0.ogg", target_peak=0.89)

    b1, _ = fetch(impact_base + "impactMetal_heavy_001.ogg")
    save_ogg(apply_fade(to_stereo(b1), in_s=0.001, out_s=0.03), "sfx_bump_1.ogg", target_peak=0.89)

    b2, _ = fetch(impact_base + "impactPlate_heavy_001.ogg")
    save_ogg(apply_fade(to_stereo(b2), in_s=0.001, out_s=0.03), "sfx_bump_2.ogg", target_peak=0.89)

    # Hit (Heavy kart smash / spin-out impact)
    hit, _ = fetch(impact_base + "impactPunch_heavy_000.ogg")
    save_ogg(apply_fade(to_stereo(hit), in_s=0.002, out_s=0.04), "sfx_hit.ogg", target_peak=0.89)

    # 4. Kenney Interface Sounds (CC0)
    ui_base = "https://raw.githubusercontent.com/ETdoFresh/kenney.nl/master/kenney_interfacesounds/Audio/"
    select, _ = fetch(ui_base + "confirmation_002.ogg")
    save_ogg(apply_fade(to_stereo(select), in_s=0.002, out_s=0.02), "sfx_select.ogg", target_peak=0.88)

    print("\nAll SFX successfully downloaded, remastered, and saved!")

if __name__ == "__main__":
    main()
