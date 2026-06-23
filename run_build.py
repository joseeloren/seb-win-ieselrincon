import subprocess
import sys

msbuild = r"C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe"
args = [msbuild, "/t:Rebuild", "/p:Configuration=Release", "/p:Platform=x64", "SafeExamBrowser.sln"]

result = subprocess.run(args)
sys.exit(result.returncode)
