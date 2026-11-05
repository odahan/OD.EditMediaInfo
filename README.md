# odmedia — Media Info Editor CLI (.NET 10 win-x64)

Self-contained Windows x64 executable for inspecting audio, video, and photographs and editing supported metadata. The interactive terminal workspace uses sections, search, a current/new-value grid, and grouped saves. Photo metadata extraction is built in. Extended audio/video analysis uses optional FFprobe, installable directly from the application without administrator rights.

## Version 2 workspace

### Offline user manual (English / French)

Press **F1** or choose **F4 → User manual** to extract and open the embedded HTML guide. F1 also works while entering a file path or editing a value. Both manuals use a dark screen theme, include their own CSS, and work offline without JavaScript, external fonts, or other assets. A light print layout is provided for paper.

```powershell
odmedia manual --lang en
odmedia manual --lang fr
odmedia manual --lang fr --no-open
```

Calling help extracts `odmedia-manual-en.html` and `odmedia-manual-fr.html` beside the real executable, making the language links work offline. The selected guide opens with the default HTML application unless `--no-open` is specified. The default language is French for a French system UI, otherwise English. Existing generated guides are refreshed when their embedded content changes; keep personal notes in another file. If the executable folder is protected, move odmedia.exe to a writable folder and retry. Standard `--help` continues to show command syntax.

Open a file with `odmedia "path to media"`, or launch `odmedia` and paste or drag a file path. The workspace shows a short overview and only the sections relevant to that file: General, Video, Audio, Image, Capture, Location, Subtitles, Chapters, Metadata, Images, Color, and Advanced.

| Key | Action |
|-----|--------|
| Tab / Shift+Tab, Left / Right | Next / previous section |
| F2 | Choose a section |
| F1 | Extract and open the English/French offline manual |
| Up / Down, Page Up / Page Down, Home / End | Navigate the current grid |
| Enter | Edit the selected supported field, or inspect the complete read-only value and its source |
| `/` or Ctrl+F | Search field names, values, and sources across all sections; empty input clears the search |
| F3 | Show only pending changes across sections |
| Delete | Stage an explicit field removal |
| Ctrl+Z | Revert the selected field |
| Ctrl+S or F6 | Review and save all pending changes |
| F4 | Actions: artwork, presets, export, analyzer installation, Explorer integration, backup settings |
| Ctrl+O | Open another file |
| Esc | Quit, with Save / Discard / Cancel when there are pending changes |

Each editable field has a **Keep**, **Replace**, or **Remove** state. Empty or whitespace-only editor input leaves its pending state unchanged; an invisible space never deletes a field. Use Delete to remove and Ctrl+Z to restore. Editors support Ctrl+A to clear input, Ctrl+Enter for multiline text, and Esc to cancel. Lists use semicolons; numeric values use a decimal point; exposure accepts fractions such as `1/125`; camera dates use `yyyy-MM-dd HH:mm:ss` without assuming a timezone.

Navigation, searching, and preset previews preserve pending changes. A preset is staged in the grid before saving. Artwork replacement/removal is also staged. Export and preset creation from the file use **saved values**, excluding pending edits. Save writes a temporary sibling file, reopens it, verifies the grid fields survived the round trip, then atomically replaces the original. A format that silently ignores a requested grid edit causes the save to fail while preserving the original and the pending edits. The first `.bak` is preserved. Backup and modification-time preservation can be configured from Actions.

The terminal must be at least 76 columns × 18 rows. Long values are shortened in the grid; Enter opens a scrollable complete-value view for read-only properties. Interactive mode requires a terminal; use `info --json` when redirecting output.

## Inspection coverage

| Media | Information |
|-------|-------------|
| Video | Stored resolution, exact rational average/nominal frame rates, separate codecs and bitrates, aspect ratios, rotation, profile/level, pixel format, bit depth when declared, scan order, color properties, HDR side data, declared frame counts, track durations/start times, language/default/forced flags, subtitles, and chapters |
| Audio | Separate track codecs, bitrate, sample rate, sample precision, channel layout, language/default flags, lossless codec identification where known, and available ReplayGain tags; editable metadata also includes grouping, conductor, and tempo |
| Photo | Dimensions/megapixels, orientation, camera, lens, actual/35 mm-equivalent focal length, exposure time, aperture, ISO, exposure compensation, metering, flash, white balance, original dates/offsets, GPS, color/ICC, printing resolution, and available EXIF/IPTC/XMP/manufacturer data |
| Artwork | Actual encoded dimensions, MIME type, size, description, and picture type |

Information is reported only when available from the file and selected reader. Missing properties are not fabricated. Full FFprobe stream/format/side-data results are retained in JSON; the Advanced section exposes their source fields. XMP paths and manufacturer-specific photo tags retain their source names so conflicting metadata remains distinguishable.

Cadence mode cannot reliably be established from average/nominal header rates. Use **Actions → Analyze cadence** or `odmedia info video.mp4 --analyze-cadence` to inspect up to 300 frames from the first video track. Results explicitly distinguish observed variable timing from uniform timing in the sample; they do not certify the full file as constant-rate. Header frame counts are labelled as declared. Size/duration bitrate estimates include container overhead and are labelled as estimates.

TagLibSharp edits supported audio/video tags. Photo writes are restricted to JPEG, PNG, and TIFF files recognized by its image writer; capture and GPS fields are initially locked and can be enabled explicitly from Actions, where supported changes must survive read-back verification. Other images and RAW formats are **inspection only**. RAW coverage depends on the format and manufacturer; unsupported or damaged data is reported through analysis notices. A listed property does not imply write support. Editing codec, dimensions, or frame rate would require transcoding and is outside metadata editing.

## Optional media analyzer

```powershell
odmedia tools status
odmedia tools install
```

The same installer is available from F4 → **Install media analyzer**, including a recovery action when opening an unsupported media file without FFprobe. It downloads the Windows x64 FFmpeg essentials ZIP from [Gyan's FFmpeg builds](https://www.gyan.dev/ffmpeg/builds/), verifies the published SHA-256, and installs `ffprobe.exe` and `ffmpeg.exe` **beside `odmedia.exe`**. Existing executables are preserved. Source/license information is saved alongside the tools. The download requires Internet access and approximately 110 MB; installed sizes vary by release. No administrator rights or PATH changes are needed. Move odmedia.exe to a writable folder if its current folder is protected. Esc cancels an interactive download.

Analyzer discovery order: explicit `ODMEDIA_FFPROBE` executable path, `ffprobe.exe` beside odmedia.exe, then PATH. An invalid explicit override is reported as unavailable; remove or correct it to use automatic detection. Without FFprobe, native TagLib information and built-in photo inspection remain available, with a notice when extended analysis is missing. Probe processes use local-file access only and a 30-second timeout.

## Explorer context menu

```powershell
odmedia explorer install
odmedia explorer remove
```

These actions are also available under F4. They add/remove **Inspect metadata with odmedia** for the current user, without administrator rights or changes to default file associations. The entry is available for files; unsupported types display an explanatory error. It opens a single selected file in the interactive terminal. On Windows 11, the classic entry is under **Show more options**. Run installation from odmedia.exe in its permanent location; register again after moving it. Registration is optional and is never performed automatically on startup.

## Installation

**For immediate use on Windows x64, just download `odmedia.exe` from [Releases](https://github.com/odahan/OD.EditMediaInfo/releases) — no build required.**

1. Go to **Releases** → download `odmedia.exe` (self-contained; size depends on the release)
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

Flow: file → overview → contextual sections and searchable grid → stage multiple edits → review all changes → save once (creates `.bak` by default).

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

Cover is limited to 10 MB. No resize/transcode — image is embedded as-is. Larger files are rejected. Preset covers are stored inline base64 in JSON (optional, with size warning >5 MB).

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
| `--analyze-cadence` | Inspect up to 300 frames from the first video track; explicitly sampled results |

Existing top-level JSON fields remain available, including the legacy audio-only `bitrate`. Extended results are additive under `details` (`Kind`, `Container`, `OverallBitrate` in bit/s, capability flags, source-labelled `Properties`, `Streams`, `Chapters` with seconds, `Diagnostics`, and the complete `ProbeData`). Track JSON retains the FFprobe field names and units.

## Build

```powershell
dotnet build src/OD.EditMediaInfo -c Release -r win-x64 -m:1
dotnet publish src/OD.EditMediaInfo -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true -o publish -m:1
.\publish\odmedia.exe --help
```

Prerequisites: .NET SDK 10.0.400 or compatible newer SDK. Dependencies include Spectre.Console 0.49.1, TagLibSharp 2.3.0, MetadataExtractor 2.9.3, and CommunityToolkit.Mvvm 8.4.0.

Run tests with `dotnet test OD.EditMediaInfo.slnx -m:1`.

## Structure

```
src/OD.EditMediaInfo/
  OD.EditMediaInfo.csproj (AssemblyName odmedia, SelfContained, SingleFile, v2.0.2)
  Program.cs (dispatch + interactive detection)
  Commands/InfoCommand, SetCommand, ClearCommand, PresetCommand
  Services/IMediaTagService, MediaTagService, MediaProbeService, InteractiveEditor, PresetService, ErrorHandler
  Models/MediaInfo, EditOptions, Preset
  Models/MediaDetails (inspection properties, streams, chapters, capabilities)
  ViewModels/EditorViewModel, EditorField (MVVM Toolkit, pending changes)
  Views/TerminalEditorView (bounded terminal grid)
  Services/MediaInspectionService, PhotoMetadataService, FFprobeService, TagFieldCatalog, MediaToolsService
```
