import os
import re

def replace_in_file(filepath, replacements):
    try:
        with open(filepath, 'r', encoding='utf-8') as f:
            content = f.read()
            
        new_content = content
        for pattern, repl in replacements:
            new_content = re.sub(pattern, repl, new_content, flags=re.IGNORECASE)
            
        if new_content != content:
            with open(filepath, 'w', encoding='utf-8') as f:
                f.write(new_content)
    except Exception as e:
        try:
            with open(filepath, 'r', encoding='windows-1252') as f:
                content = f.read()
            new_content = content
            for pattern, repl in replacements:
                new_content = re.sub(pattern, repl, new_content, flags=re.IGNORECASE)
            if new_content != content:
                with open(filepath, 'w', encoding='windows-1252') as f:
                    f.write(new_content)
        except:
            pass

def bulk_rebrand_author(root_dir):
    replacements = [
        (r'Jose Francisco Lorenzo Hernández', 'Dpto. Informática IES El Rincón'),
        (r'Jose Francisco Lorenzo Hern\\?ndez', 'Dpto. Informática IES El Rincón'),
        (r'Jose Francisco', 'Dpto. Informática IES El Rincón')
    ]
    
    for dirpath, dirnames, filenames in os.walk(root_dir):
        if '.git' in dirpath or 'bin' in dirpath or 'obj' in dirpath or 'packages' in dirpath:
            continue
        for filename in filenames:
            if filename.endswith(('.cs', '.wxs', '.wxl', '.resx')):
                filepath = os.path.join(dirpath, filename)
                replace_in_file(filepath, replacements)

if __name__ == '__main__':
    bulk_rebrand_author('.')
