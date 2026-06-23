import os
from PIL import Image

source_image_path = r"C:\Users\Jose\.gemini\antigravity-ide\brain\4de36729-dc6d-4056-b388-77f77b8dba72\el_arrinconador_logo_1782064281719.png"
base_dir = r"c:\Users\Jose\workspace\seb-win-ieselrincon"

img = Image.open(source_image_path).convert("RGBA")

# 1. SplashScreen (550x300)
# Create a 550x300 white or dark canvas and center the logo
splash = Image.new("RGBA", (550, 300), (255, 255, 255, 255)) # White background
logo_splash = img.resize((200, 200), Image.Resampling.LANCZOS)
splash.paste(logo_splash, ((550-200)//2, (300-200)//2), logo_splash)
for p in [r"SafeExamBrowser.UserInterface.Desktop\Images\SplashScreen.png", r"SafeExamBrowser.UserInterface.Mobile\Images\SplashScreen.png"]:
    splash.save(os.path.join(base_dir, p))
    print(f"Saved {p}")

# 2. Bundle Logo (64x64)
bundle_logo = img.resize((64, 64), Image.Resampling.LANCZOS)
bundle_logo.save(os.path.join(base_dir, r"SetupBundle\Resources\Logo.png"))
print("Saved SetupBundle Logo")

# 3. Setup Banner (493x58) BMP
banner = Image.new("RGB", (493, 58), (255, 255, 255))
logo_banner = img.resize((48, 48), Image.Resampling.LANCZOS)
banner.paste(logo_banner, (493 - 58, 5), logo_banner if logo_banner.mode == 'RGBA' else None)
banner.save(os.path.join(base_dir, r"Setup\Resources\Banner.bmp"))
print("Saved Banner.bmp")

# 4. Setup Dialog (493x312) BMP
dialog = Image.new("RGB", (493, 312), (255, 255, 255))
logo_dialog = img.resize((150, 150), Image.Resampling.LANCZOS)
dialog.paste(logo_dialog, (20, 20), logo_dialog if logo_dialog.mode == 'RGBA' else None)
dialog.save(os.path.join(base_dir, r"Setup\Resources\Dialog.bmp"))
print("Saved Dialog.bmp")

# 5. Icons (.ico)
# Need 16, 32, 48, 64, 128, 256 sizes for standard Windows icons
icon_sizes = [(16,16), (32,32), (48,48), (64,64), (128,128), (256,256)]
icon_targets = [
    r"SafeExamBrowser.Client\SafeExamBrowser.ico",
    r"SafeExamBrowser.ResetUtility\ResetUtility.ico",
    r"SafeExamBrowser.Runtime\SafeExamBrowser.ico",
    r"SafeExamBrowser.Service\SafeExamBrowser.ico",
    r"SafeExamBrowser.UserInterface.Desktop\Images\LogNotification.ico",
    r"SafeExamBrowser.UserInterface.Desktop\Images\SafeExamBrowser.ico",
    r"SafeExamBrowser.UserInterface.Mobile\Images\LogNotification.ico",
    r"SafeExamBrowser.UserInterface.Mobile\Images\SafeExamBrowser.ico",
    r"SebWindowsConfig\ConfigurationTool.ico",
    r"Setup\Resources\Application.ico",
    r"Setup\Resources\ConfigurationFile.ico",
    r"Setup\Resources\ConfigurationTool.ico",
    r"Setup\Resources\ResetUtility.ico"
]

for t in icon_targets:
    img.save(os.path.join(base_dir, t), format='ICO', sizes=icon_sizes)
    print(f"Saved {t}")

print("All images generated and replaced successfully.")
