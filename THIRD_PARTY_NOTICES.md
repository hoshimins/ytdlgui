# Third-party software and distribution notes

This repository builds Movie Downloader but does not include or publish `yt-dlp.exe`, `ffmpeg.exe`, or `ffprobe.exe` as part of the application artifact.

## yt-dlp

- Project: https://github.com/yt-dlp/yt-dlp
- Source license: The Unlicense
- Important binary note: the official PyInstaller-bundled Windows executables include third-party GPLv3+ code. The yt-dlp project documents the combined executable as GPLv3+.
- Local setup: `setup-tools.ps1` downloads the executable directly from the official yt-dlp GitHub Release.

## FFmpeg

- Project: https://ffmpeg.org/
- Windows build source used by the local setup script: Gyan's official GitHub mirror at https://github.com/GyanD/codexffmpeg/releases (documented at https://www.gyan.dev/ffmpeg/builds/)
- FFmpeg is LGPLv2.1-or-later by default, but optional GPL components change the resulting build's license.
- Gyan's static Windows builds are documented as GPLv3. The setup script downloads the Essentials Build directly from Gyan and installs its `ffmpeg.exe` and `ffprobe.exe`.

## .NET runtime

- Project: https://github.com/dotnet/runtime
- License: MIT
- The self-contained publish contains Microsoft .NET runtime components. Preserve the copyright and permission notices required by the MIT license when distributing the application.

## Redistribution policy for this project

The default build output contains the application only. `setup-tools.ps1` is intended to prepare a local working copy after build. Do not redistribute a folder containing the downloaded GPL executables unless you separately satisfy all applicable license requirements, including corresponding-source and license-notice obligations.

This document is an engineering compliance note, not legal advice. Patent and website terms may impose additional requirements depending on jurisdiction and use.
