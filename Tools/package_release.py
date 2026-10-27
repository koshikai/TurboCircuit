"""
Package Turbo Circuit Windows 64-bit standalone build into TurboCircuit-win64.zip
Excludes backup/debug folders and temporary verification files.
"""

import os
import zipfile

def package_release():
    build_dir = os.path.abspath("Build/Windows")
    output_zip = os.path.abspath("TurboCircuit-win64.zip")
    
    if os.path.exists(output_zip):
        os.remove(output_zip)
        
    print(f"Packaging {build_dir} -> {output_zip}...")
    
    exclude_dirs = {
        "turbocircuit_backupthisfolder_butdontshipitwithyourgame",
    }
    
    with zipfile.ZipFile(output_zip, 'w', zipfile.ZIP_DEFLATED, compresslevel=9) as zf:
        for root, dirs, files in os.walk(build_dir):
            # Filter out excluded directories
            dirs[:] = [d for d in dirs if d.lower() not in exclude_dirs]
            
            for file in files:
                # Exclude verification pngs (verify_*, AutoShot の shot_*) and model bounds
                if file.startswith(("verify_", "shot_")) or file.endswith(".pdb"):
                    continue
                    
                full_path = os.path.join(root, file)
                rel_path = os.path.relpath(full_path, build_dir)
                zf.write(full_path, rel_path)
                
    size = os.path.getsize(output_zip)
    print(f"Successfully packaged {output_zip}: {size:,} bytes ({size / 1024 / 1024:.2f} MB)")
    return size

if __name__ == "__main__":
    package_release()
