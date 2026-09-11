import os
import re
import sys

def replace_in_file(filepath, pattern, repl):
    try:
        # Las fuentes históricas mezclan UTF-8 y Windows-1252; la versión es ASCII.
        with open(filepath, 'rb') as f:
            content = f.read()
        new_content = re.sub(pattern.encode('ascii'), repl.encode('ascii'), content)
        if new_content != content:
            with open(filepath, 'wb') as f:
                f.write(new_content)
    except OSError as error:
        raise RuntimeError(f'No se pudo actualizar {filepath}') from error

def bulk_bump_version(root_dir, old_v, new_v):
    pattern = old_v.replace('.', r'\.')
    repl = new_v
    for dirpath, dirnames, filenames in os.walk(root_dir):
        if '.git' in dirpath or 'bin' in dirpath or 'obj' in dirpath or 'packages' in dirpath:
            continue
        for filename in filenames:
            if filename.endswith(('.cs', '.wxs', '.wxl', '.resx')):
                filepath = os.path.join(dirpath, filename)
                replace_in_file(filepath, pattern, repl)

if __name__ == '__main__':
    if len(sys.argv) != 3:
        print("Usage: python bump_version.py <old_version> <new_version>")
        sys.exit(1)
    bulk_bump_version('.', sys.argv[1], sys.argv[2])
