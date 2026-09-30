import os
from PIL import Image
import numpy as np

def make_transparent_from_black(src_path, dst_path, threshold=15, soft_range=35):
    img = Image.open(src_path).convert("RGBA")
    arr = np.array(img, dtype=np.float32)
    
    r, g, b, a = arr[:, :, 0], arr[:, :, 1], arr[:, :, 2], arr[:, :, 3]
    brightness = np.maximum(np.maximum(r, g), b)
    
    # Smooth alpha ramp
    alpha = np.clip((brightness - threshold) / max(soft_range, 1), 0.0, 1.0) * 255.0
    
    arr[:, :, 3] = alpha
    result = Image.fromarray(arr.astype(np.uint8))
    
    # Crop bounding box of visible content
    bbox = result.getbbox()
    if bbox:
        # Add a tiny margin
        w, h = result.size
        bbox = (max(0, bbox[0]-4), max(0, bbox[1]-4), min(w, bbox[2]+4), min(h, bbox[3]+4))
        result = result.crop(bbox)
        
    os.makedirs(os.path.dirname(dst_path), exist_ok=True)
    result.save(dst_path, "PNG")
    print(f"Saved: {dst_path} (size: {result.size})")

artifacts_dir = r"C:\Users\kaito\.gemini\antigravity\brain\ea3e96f8-449c-46b2-9125-6b1ec90f54c6"
out_dir = r"c:\Users\kaito\UnityProjects\TurboCircuit\Assets\Resources\UI"

tasks = [
    ("logo_turbo_circuit_1790791727728.jpg", "title_logo.png", 18, 40),
    ("badge_course_circuit_1790791743771.jpg", "badge_track_0.png", 12, 30),
    ("badge_course_dunes_1790791760089.jpg", "badge_track_1.png", 12, 30),
    ("badge_course_frost_1790791775624.jpg", "badge_track_2.png", 12, 30),
]

for src, dst, th, sr in tasks:
    src_full = os.path.join(artifacts_dir, src)
    dst_full = os.path.join(out_dir, dst)
    if os.path.exists(src_full):
        make_transparent_from_black(src_full, dst_full, th, sr)
    else:
        print(f"Missing: {src_full}")
