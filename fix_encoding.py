import os

def fix_file(path):
    try:
        with open(path, 'rb') as f:
            content = f.read()
            
        encoding_used = 'utf-8'
        try:
            text = content.decode('utf-8')
        except UnicodeDecodeError:
            try:
                text = content.decode('windows-1252')
                encoding_used = 'windows-1252'
            except UnicodeDecodeError:
                return

        new_text = text.replace('InformÃ¡tica', 'Informática')
        new_text = new_text.replace('RincÃ³n', 'Rincón')
        new_text = new_text.replace('Â©', '©')
        
        if text != new_text:
            with open(path, 'wb') as f:
                f.write(new_text.encode(encoding_used))
            print(f"Fixed {path} (encoding: {encoding_used})")
            
    except Exception as e:
        print(f"Error processing {path}: {e}")

for r, d, fs in os.walk('.'):
    if '.git' in r or 'obj' in r or 'bin' in r or 'packages' in r:
        continue
    for f in fs:
        if f.endswith(('.cs', '.wxs', '.resx', '.xaml', '.wxl')):
            fix_file(os.path.join(r, f))
