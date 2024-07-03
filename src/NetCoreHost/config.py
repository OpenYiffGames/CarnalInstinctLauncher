from typing import Final
import os
import requests
import zipfile
import io
from shutil import copyfileobj

# Constants
NUGET_API_URL: Final = "https://www.nuget.org/api/v2/package"
PROJECT_PATH: Final = os.path.dirname(__file__)
INCLUDES_FOLDER: Final = os.path.join(PROJECT_PATH, "include")
LIB_FOLDER: Final = os.path.join(PROJECT_PATH, "lib")

# Nuget package names
NETCORE_HOST_WIN_X64: Final = "Microsoft.NETCore.App.Host.win-x64"
NETCORE_HOST_WIN_X86: Final = "Microsoft.NETCore.App.Host.win-x86"
PKG_VERSION = "8.0.6"

def download_nuget_package(package_name, version) -> bytes:
    url = f"https://www.nuget.org/api/v2/package/{package_name}/{version}"
    response = requests.get(url)
    
    if response.status_code == 200:
        print(f"Downloaded {package_name} version {version}")
    else:
        print(f"Failed to download {package_name} version {version}")
        response.raise_for_status()

    return response.content

def write_file(file_name, data):
    os.makedirs(os.path.dirname(file_name), exist_ok=True)
    with open(file_name, "wb") as f:
        copyfileobj(data, f)
    
if __name__ == "__main__":

    package_name = NETCORE_HOST_WIN_X64
    version = PKG_VERSION
    pkg = download_nuget_package(package_name, version)

    with zipfile.ZipFile(io.BytesIO(pkg)) as z:
        for f in z.namelist():
            file_name = os.path.basename(f)
            if f.endswith(".h"):
                print(f"Extracted {f}")
                file = z.open(f)
                write_file(os.path.join(INCLUDES_FOLDER, file_name), file)
            elif f.endswith('.dll') or f.endswith('.lib') or f.endswith('.pdb'):
                print(f"Extracted {f}")
                file = z.open(f)
                write_file(os.path.join(LIB_FOLDER, file_name), file)
        
