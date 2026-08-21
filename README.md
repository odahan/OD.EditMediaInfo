# odmedia — Media Info Editor CLI (.NET 10 win-x64)

Self-contained Windows x64 executable. No external dependencies. Edit metadata for MP3, MP4/M4A, FLAC, OGG, Opus, WMA, WAV, AIFF, etc. via TagLibSharp.

## Installation

**For immediate use on Windows x64, just download `odmedia.exe` from [Releases](https://github.com/odahan/OD.EditMediaInfo/releases) — no build required.**

1. Go to **Releases** → download `odmedia.exe` (~10 MB, self-contained)
2. Run directly: double-click or `odmedia.exe --help`
3. Optional: add to PATH

> Building from source is only needed for development. See [Build](#build) below.

## License and support

OD.EditMediaInfo is distributed under a [Donationware License](LICENSE.md).

Copyright © 2026 Olivier Dahan. If the project is useful to you, you can support its development at [Buy Me a Coffee](https://buymeacoffee.com/knhsynths).

Source repository: https://github.com/odahan/OD.EditMediaInfo

## Usage

### Interactive mode (viewer + editor)

```powershell
odmedia song.mp3             # show info + menu to edit
odmedia                      # prompt for file (drag & drop)
odmedia "C:\Music\track.m4a" # MP4/M4A also supported
```

Flow: file → table Technical (format, duration, bitrate, codec, channels, sample rate) + table Tags (title, artist, album, genre, year, track/disc, comment, composers) + cover summary → menu: pick field to edit → enter new value → confirm → save (creates `.bak`) → loop.

### Command line

```powershell
# Show info
odmedia info track.mp3
odmedia info track.mp4 --json

# Edit tags
odmedia set track.mp3 --title "New Title" --artist "Artist" --album "Album"
odmedia set track.mp3 --title "T" --artist "A;B" --genre "Rock;Pop" --year 2024 --track 5 --track-count 12
odmedia set track.mp3 --comment "My comment" --composer "Composer" --copyright "©2024 Label" --publisher "My Label"
odmedia set track.mp3 --cover cover.jpg
odmedia set track.mp3 --remove-cover
odmedia set track.mp3 --title "T" --cover cover.jpg --no-backup --keep-mtime --dry-run

# Presets — stored in <exeDir>/presets/*.json
odmedia preset save track.mp3 --name "MonLabel"                          # from file, checkbox-filtered in interactive, auto-all in CLI
odmedia preset save --name "MonLabel" --artist "John" --genre "Rock" --copyright "©2024" --publisher "Label" --cover cover.jpg
odmedia preset save track.mp3 --name "MonLabel" --force                  # overwrite an existing preset
odmedia preset list
odmedia preset show "MonLabel" --json
odmedia preset apply other.mp3 --name "MonLabel" --dry-run
odmedia preset apply other.mp3 --name "MonLabel" --no-backup --keep-mtime
odmedia preset delete "MonLabel" --yes

# Clear all tags
odmedia clear track.mp3 --yes

# Help / version
odmedia --help
odmedia --version
```

### `set` options

| Option | Description | Default |
|--------|-------------|---------|
| `--title` | Title | - |
| `--artist` | Artist(s), `;` separated for multiple | - |
| `--album` | Album | - |
| `--album-artist` | Album artist(s), `;` separated | - |
| `--genre` | Genre(s), `;` separated | - |
| `--year` | Year (e.g. `2024`) | - |
| `--track` | Track number | - |
| `--track-count` | Total tracks | - |
| `--disc` | Disc number | - |
| `--disc-count` | Total discs | - |
| `--comment` | Comment | - |
| `--composer` | Composer(s), `;` separated | - |
| `--copyright` | Copyright | - |
| `--publisher` | Publisher / Label | - |
| `--cover` | Cover image file (jpg/png, max 10 MB) | - |
| `--remove-cover` | Remove all embedded covers | `false` |
| `--cover-desc` | Cover description | `Front Cover` |
| `--backup / --no-backup` | Create `.bak` backup | `--backup` |
| `--keep-mtime` | Preserve file modification time | `false` |
| `--dry-run` | Show what would be done without saving | `false` |

Cover is limited to 10 MB. No resize/transcode in V1 — image is embedded as-is. Larger files are rejected. Preset covers are stored inline base64 in JSON (optional, with size warning >5 MB).

ID3v2.3 is forced for MP3 for Windows Media Player compatibility.

Exit codes: `0 OK`, `1 args/file`, `2 tag error`, `130 cancelled Ctrl+C`.

### Presets

* Location: `<exeDir>/presets/*.json` by default (created on demand). Set `ODMEDIA_PRESET_DIR` to use a different preset folder.
* Interactive: file → menu `Preset — Create from this file` (multi-select checkboxes per field) / `Create from scratch` / `Apply to this file` / `Manage (list/delete)`.
* Generic fields only: `Artist, Album, AlbumArtist, Genre, Year, Composer, Comment, Copyright, Publisher, Cover(optional)` — `Title/Track/Disc` excluded by design. `EncodedBy` not available via TagLibSharp 2.3.0 and therefore not included.
* Cover is optional and limited to 10 MB; the interactive workflow warns when it exceeds 5 MB.

### `info` options

| Option | Description |
|--------|-------------|
| `--json` | Output raw JSON |

## Build

```powershell
dotnet build src/OD.EditMediaInfo -c Release -r win-x64 -m:1
dotnet publish src/OD.EditMediaInfo -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true -o publish -m:1
.\publish\odmedia.exe --help
```

Prerequisites: .NET SDK 10.0.400, Spectre.Console 0.49.1, TagLibSharp 2.3.0

## Structure

```
src/OD.EditMediaInfo/
  OD.EditMediaInfo.csproj (AssemblyName odmedia, SelfContained, SingleFile, v1.3.0)
  Program.cs (dispatch + interactive detection)
  Commands/InfoCommand, SetCommand, ClearCommand, PresetCommand
  Services/IMediaTagService, MediaTagService, MediaProbeService, InteractiveEditor, PresetService, ErrorHandler
  Models/MediaInfo, EditOptions, Preset
```
