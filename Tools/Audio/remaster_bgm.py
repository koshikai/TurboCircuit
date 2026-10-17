"""
Remaster all course BGM tracks for Turbo Circuit using FFmpeg.
- Fixes bgm_3.ogg (192,000 Hz -> 44,100 Hz pristine resampling).
- Converts mono tracks (bgm_4.ogg, bgm_5.ogg) into rich stereo using Haas effect spatialization.
- Normalizes peak and loudness across all tracks (0.90 peak / -0.9dB) so tracks sound balanced and clear.
"""

import os
import subprocess
import soundfile as sf
import numpy as np

AUDIO_DIR = os.path.abspath("Assets/Resources/Audio")

def get_peak_and_channels(filepath):
    data, sr = sf.read(filepath)
    peak = np.max(np.abs(data))
    channels = 1 if len(data.shape) == 1 else data.shape[1]
    return peak, channels, sr

def remaster_track(index):
    filename = f"bgm_{index}.ogg"
    filepath = os.path.join(AUDIO_DIR, filename)
    tmp_path = os.path.join(AUDIO_DIR, f"tmp_{filename}")
    
    if not os.path.exists(filepath):
        return
        
    peak, channels, sr = get_peak_and_channels(filepath)
    print(f"[{filename}] Initial: sr={sr}Hz, ch={channels}, peak={peak:.3f}")
    
    filters = []
    
    # 1. Resample if not 44.1kHz
    # 2. Stereo widening if mono
    if channels == 1:
        # Haas effect: left channel direct, right channel delayed by 10ms with slight warmth
        # stereo filter: split mono into 2 channels, delay right by 10ms
        filters.append("asplit[l][r];[r]adelay=10|10[rdel];[l][rdel]amerge=inputs=2")
        
    # Calculate target gain to hit 0.90 peak
    # target_gain = 0.90 / max(peak, 0.05)
    target_peak = 0.90
    gain_factor = target_peak / max(peak, 0.01)
    # Clamp gain between 0.8 and 2.5 to prevent extreme amplification
    gain_factor = max(0.8, min(2.5, gain_factor))
    filters.append(f"volume={gain_factor:.3f}")
    
    cmd = [
        "ffmpeg", "-y", "-i", filepath,
        "-ar", "44100",
        "-ac", "2",
        "-filter_complex" if channels == 1 else "-filter:a",
        ",".join(filters),
        "-c:a", "libvorbis",
        "-q:a", "6",
        tmp_path
    ]
    
    res = subprocess.run(cmd, capture_output=True, text=True)
    if res.returncode != 0:
        print(f"Error remastering {filename}: {res.stderr}")
        if os.path.exists(tmp_path):
            os.remove(tmp_path)
        return
        
    # Replace original
    os.replace(tmp_path, filepath)
    new_peak, new_ch, new_sr = get_peak_and_channels(filepath)
    print(f"[{filename}] Finished: sr={new_sr}Hz, ch={new_ch}, peak={new_peak:.3f}\n")

def main():
    for i in range(6):
        remaster_track(i)

if __name__ == "__main__":
    main()
