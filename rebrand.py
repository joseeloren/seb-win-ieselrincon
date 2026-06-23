import os
import re

print("Starting replacement script...")

def replace_in_file(filepath, pattern, replacement):
    encodings = ['utf-8', 'windows-1252', 'latin-1']
    content = None
    used_encoding = None
    for enc in encodings:
        try:
            with open(filepath, 'r', encoding=enc) as f:
                content = f.read()
            used_encoding = enc
            break
        except UnicodeDecodeError:
            continue
            
    if content is None:
        print(f"Skipping {filepath} due to encoding issues.")
        return

    new_content, count = re.subn(pattern, replacement, content)
    
    if count > 0:
        with open(filepath, 'w', encoding=used_encoding) as f:
            f.write(new_content)
        print(f"Updated: {filepath} ({count} replacements)")

for root, dirs, files in os.walk('.'):
    # Skip .git, .vs, packages, obj, bin
    dirs[:] = [d for d in dirs if d not in ['.git', '.vs', 'packages', 'obj', 'bin', 'Resources', 'Images', 'TabIcons']]
    
    for file in files:
        if file == 'AssemblyInfo.cs':
            filepath = os.path.join(root, file)
            # Replace Copyright
            replace_in_file(filepath, r'AssemblyCopyright\(.*?\)', 'AssemblyCopyright("Copyright © 2026 Jose Francisco Lorenzo Hernández")')
            replace_in_file(filepath, r'AssemblyCompany\(.*?\)', 'AssemblyCompany("Jose Francisco Lorenzo Hernández")')
            replace_in_file(filepath, r'AssemblyTitle\("Safe Exam Browser', 'AssemblyTitle("El Rincón Seguro')
            replace_in_file(filepath, r'AssemblyProduct\("Safe Exam Browser', 'AssemblyProduct("El Rincón Seguro')
            
        elif file.endswith(('.cs', '.xaml', '.resx', '.wxs', '.wixproj', '.md', '.txt')):
            filepath = os.path.join(root, file)
            # Be very careful not to replace SafeExamBrowser (no spaces) inside namespaces.
            replace_in_file(filepath, r'Safe Exam Browser', 'El Rincón Seguro')

print("Replacement complete.")
