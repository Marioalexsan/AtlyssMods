import os
import sys
import glob
import subprocess
import re
import shutil
import zipfile
import json

def keep_assembly_dependency(assembly):
  WHITELISTED_ASSEMBLIES = [
    "Microsoft.Win32.Registry",
    "System.Memory",
    "Sustem.Buffers",
    "System.Runtime.CompilerServices.Unsafe",
    "System.ValueTuple"
  ]

  IGNORED_ASSEMBLIES = [
    "AsmResolver",
    "BepInEx.AssemblyPublicizer",
    "Newtonsoft.Json",
    "UnityEngine.",
    "EasySettings",
    "System."
  ]

  for part in WHITELISTED_ASSEMBLIES:
    if part in assembly:
      return True

  for part in IGNORED_ASSEMBLIES:
    if part in assembly:
      return False

  return True


LAST_WORKDIR = os.getcwd()

# Ensure script location is current directory
os.chdir(os.path.dirname(os.path.abspath(__file__)))

if len(sys.argv) < 2:
  print('Expected path argument')
  sys.exit(1)

os.chdir('..')
os.chdir(sys.argv[1])

csprojs = glob.glob("*.csproj")

if len(csprojs) != 1:
  print("Couldn't determine solution file to build.")
  sys.exit(1)

with open(csprojs[0], 'r') as f:
  CSPROJ_DATA = f.read()
  MATCH = re.search("<TargetFramework>(.*)</TargetFramework>", CSPROJ_DATA)

  if MATCH is not None:
    if MATCH.group(1) == 'netstandard2.1':
      FRAMEWORK = 'netstandard2.1'
    elif MATCH.group(1) == 'net47':
      FRAMEWORK = 'net47'
    elif MATCH.group(1) == 'net471':
      FRAMEWORK = 'net471'
    elif MATCH.group(1) == 'net472':
      FRAMEWORK = 'net472'
    elif MATCH.group(1) == 'net48':
      FRAMEWORK = 'net48'
    elif MATCH.group(1) == 'net481':
      FRAMEWORK = 'net481'
    else:
      # Let's assume it's netstandard2.1
      FRAMEWORK = 'netstandard2.1'
  else:
    # Let's assume it's netstandard2.1
    FRAMEWORK = 'netstandard2.1'

  MATCH_GUID = re.search("<AssemblyName>(.*)</AssemblyName>", CSPROJ_DATA)
    
  if MATCH_GUID is not None:
    MOD_GUID = MATCH_GUID.group(1)
  else:
    print('Failed to find <AssemblyName> to use as mod GUID in .csproj, cannot pack mod as a result.')
    sys.exit(1)

  MATCH_THUNDERSTORENAME = re.search("<ThunderstoreName>(.*)</ThunderstoreName>", CSPROJ_DATA)
    
  if MATCH_THUNDERSTORENAME is not None:
    MOD_THUNDERSTORENAME = MATCH_THUNDERSTORENAME.group(1)
  else:
    print('Failed to find <ThunderstoreName> to use as mod name in .csproj, cannot pack mod as a result.')
    sys.exit(1)

  MATCH_VERSION = re.search("<Version>(.*)</Version>", CSPROJ_DATA)
    
  if MATCH_VERSION is not None:
    MOD_VERSION = MATCH_VERSION.group(1)
  else:
    print('Failed to find <Version> to use as mod version in .csproj, cannot pack mod as a result.')
    sys.exit(1)
    
  MATCH_RID = re.search("<RuntimeIdentifier>(.*)</RuntimeIdentifier>", CSPROJ_DATA)
    
  if MATCH_RID is not None:
    MOD_RID = MATCH_RID.group(1)
  else:
    MOD_RID = None

# os.chdir(os.path.join('bin', 'x64', 'Debug', FRAMEWORK))
os.chdir(os.path.join('bin', 'Debug', FRAMEWORK))

if MOD_RID is not None:
  os.chdir(MOD_RID)

ASSEMBLIES = [os.path.abspath(path) for path in glob.glob('*.dll') if keep_assembly_dependency(path)]
PDBS = [os.path.abspath(path) for path in glob.glob('*.pdb')]

if len(ASSEMBLIES) == 0:
  print('Couldn\'t find build files, please check that the project has been built in Debug mode.')
  sys.exit(1)

print('Will copy the following DLLs and PDBs to the package:')

for assembly in ASSEMBLIES:
  print(' - ' + assembly)

for assembly in PDBS:
  print(' - ' + assembly)

# os.chdir(os.path.join('..', '..', '..', '..', '..'))
os.chdir(os.path.join('..', '..', '..', '..'))

if MOD_RID is not None:
  os.chdir('..')

if not os.path.exists('_ThunderstoreBuild'):
  os.mkdir('_ThunderstoreBuild')

if os.path.exists(os.path.join('_ThunderstoreBuild', f'{MOD_GUID}.zip')):
  os.remove(os.path.join('_ThunderstoreBuild', f'{MOD_GUID}.zip'))

# Update mod GUID and version
if not os.path.exists(os.path.join(sys.argv[1], '_Thunderstore', 'manifest.json')):
  print("Couldn't find manifest.json in _Thunderstore, was it removed?")
  sys.exit(1)

with open(os.path.join(sys.argv[1], '_Thunderstore', 'manifest.json'), 'r') as f:
  MANIFEST_DATA = json.loads(f.read())

MANIFEST_DATA['name'] = MOD_THUNDERSTORENAME
MANIFEST_DATA['version_number'] = MOD_VERSION

with open(os.path.join(sys.argv[1], '_Thunderstore', 'manifest.json'), 'w') as f:
  f.write(json.dumps(MANIFEST_DATA, indent=2))

# Pack everything into a zip

with zipfile.ZipFile(os.path.join('_ThunderstoreBuild', f'{MOD_GUID}.zip'), 'w', zipfile.ZIP_DEFLATED) as pkg:
  with open(os.path.join(sys.argv[1], '_Thunderstore', 'manifest.json'), 'rb') as f:
    pkg.writestr('manifest.json', f.read())
  with open(os.path.join(sys.argv[1], '_Thunderstore', 'icon.png'), 'rb') as f:
    pkg.writestr('icon.png', f.read())
  with open(os.path.join(sys.argv[1], '_Thunderstore', 'CHANGELOG.md'), 'rb') as f:
    pkg.writestr('CHANGELOG.md', f.read())
  with open(os.path.join(sys.argv[1], '_Thunderstore', 'README.md'), 'rb') as f:
    pkg.writestr('README.md', f.read())

  for assembly in ASSEMBLIES:
    with open(assembly, 'rb') as f:
      pkg.writestr('plugins/' + os.path.basename(assembly), f.read())

  for pdb in PDBS:
    with open(pdb, 'rb') as f:
      pkg.writestr('plugins/' + os.path.basename(pdb), f.read())

os.chdir(LAST_WORKDIR)
print('Done!')