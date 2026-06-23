import os
from PIL import Image

source_image_path = r"C:\Users\Jose\.gemini\antigravity-ide\brain\4de36729-dc6d-4056-b388-77f77b8dba72\el_arrinconador_logo_1782064281719.png"
base_dir = r"c:\Users\Jose\workspace\seb-win-ieselrincon"

img = Image.open(source_image_path).convert("RGBA")

# 3. Setup Banner (493x58) BMP
# Standard WiX banner: White background, logo on the far right
banner = Image.new("RGB", (493, 58), (255, 255, 255))
logo_banner = img.resize((48, 48), Image.Resampling.LANCZOS)
banner.paste(logo_banner, (493 - 58, 5), logo_banner if logo_banner.mode == 'RGBA' else None)
banner.save(os.path.join(base_dir, r"Setup\Resources\Banner.bmp"))
print("Saved Banner.bmp")

# 4. Setup Dialog (493x312) BMP
# Standard WiX dialog: White background for text legibility. Left strip can have logo.
dialog = Image.new("RGB", (493, 312), (255, 255, 255))
logo_dialog = img.resize((150, 150), Image.Resampling.LANCZOS)
# Place logo on the left side, vertically centered
dialog.paste(logo_dialog, (7, (312 - 150) // 2), logo_dialog if logo_dialog.mode == 'RGBA' else None)
dialog.save(os.path.join(base_dir, r"Setup\Resources\Dialog.bmp"))
print("Saved Dialog.bmp")

print("WiX Setup images (Banner and Dialog) regenerated with white background.")
