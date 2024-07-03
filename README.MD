# How does it works?
This launcher downloads the official game launcher from public sources (Official servers) and then applies a patch on the fly to bypass the paywall system

# Docs
I'm working on it (WIP). (I pretend to release some docs describing with more detail each component in the source.)

# How to build
- Have the latest version of the .Net 8.0 installed
- Visual Studio 2022
- install the python requirements in the `requirements.txt`
- run the `config.cmd` script or `src/NetCoreHost/config.py`
- Set build to `release` with `x64` target
- build the the solution

# Notes
Some antiviruses (including Windows Defender) are flagging the binary releases as malicious. Due to the suspicious nature of the installer, it downloads and unpacks multiple files required by the official launcher. Unfortunately, there's nothing I can do. You need to add the launcher to your antivirus whitelist.