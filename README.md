# OD.EditMediaInfo — Media metadata editor for Windows

OD.EditMediaInfo 3.0.0 ships two self-contained Windows x64 executables. Use `odmedia-gui.exe` for the graphical workspace and `odmedia.exe` for the CLI and interactive terminal. Both can inspect audio, video, and photographs and edit supported metadata. Photo inspection is built in. Optional FFprobe adds audio and video analysis.

## Graphical workspace

Launch `odmedia-gui.exe` and select **Open…** or drop a file onto the window. Select a section tab to browse its fields. **Search fields** and **Pending only** work across sections.

The **Preview** pane shows the photo or embedded artwork beside the grid. Artwork from presets also has a preview, and staged artwork appears alongside the saved images until you save or revert it. Preview availability depends on the image codecs installed in Windows.

Select an editable field and use **Edit…**, double-click it, or press F2. **Remove value** stages a deletion and **Revert field** discards that field’s pending change. **Save…** opens a before-and-after review. The first save keeps the original as a `.bak` file. When switching files or closing the window, choose **Save**, **Discard**, or **Cancel** if changes are pending.

On the **Metadata** tab of an editable audio or video file, use **Presets** to preview, stage, or delete a shared preset, or create one from the file’s saved tags or from scratch. Use the checkboxes to choose which tags and artwork to include when creating or staging a preset. Staged changes appear in the save review.

Use **Export → JSON, CSV, or HTML** to save the inspection report from any tab, including for files opened in read-only mode. Export includes saved values across all sections. The HTML report uses a dark theme and works offline as a single file.

## Terminal workspace


### Offline user manual (English / French)

Press **F1** or choose **F4 → User manual** to extract and open the embedded HTML guide. F1 also works while entering a file path or editing a value. Both manuals use a dark screen theme, include their own CSS, and work offline without JavaScript, external fonts, or other assets. A light print layout is provided for paper.

```powershell
odmedia manual --lang en
odmedia manual --lang fr
odmedia manual --lang fr --no-open
```

Calling help extracts `odmedia-manual-en.html` and `odmedia-manual-fr.html` beside the real executable, making the language links work offline. The selected guide opens with the default HTML application unless `--no-open` is specified. The default language is French for a French system UI, otherwise English. Existing generated guides are refreshed when their embedded content changes; keep personal notes in another file. If the executable folder is protected, move odmedia.exe to a writable folder and retry. Standard `--help` continues to show command syntax.

Open a file with `odmedia "path to media"`, or launch `odmedia` and paste a path or drag a file into the terminal. The workspace opens in **Overview** and shows the sections relevant to the file, such as **Video**, **Audio**, **Image**, and **Metadata**.

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
| Ctrl+O | Open another file (also available in F4 Actions) |
| Esc | Quit, with Save / Discard / Cancel when there are pending changes |

To switch files, press **Ctrl+O** or choose **Open another file…** in **F4 → Actions**. Drag the next file into the terminal or enter its path, then press **Enter**. If the file cannot be opened, try another path or press **Esc** at the path prompt to return to the current file. Once the next file is ready, choose **Save**, **Discard**, or **Cancel** if there are pending edits to the current file.

The editing grid separates **Saved value** and **Pending value**. Replacements are yellow, removals are red, and the selected field name is cyan. The yellow **unsaved changes** badge counts edits across sections and filters, including pending artwork changes. **Save** is highlighted in green while edits are pending. The save review shows the **Before** and **After** values.

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

In the interactive workspace, **F4 → Actions** offers **Add Explorer context menu** and **Remove Explorer context menu**. Registration adds **Inspect metadata with odmedia** for the current user without requiring administrator rights or changing the default apps. The command opens one selected file with a supported media extension. Additional formats such as MOV are registered when FFprobe is available.

**On Windows 11**, hold **Shift** while right-clicking a media file to open the classic context menu directly, then choose **Inspect metadata with odmedia**. A regular right-click opens the modern menu. From there, select **Show more options** to reach the classic menu. The modern **App extensions** submenu, when present, shows a selection of extensions supported by that menu. The current odmedia registration is for the classic menu, so its absence from that submenu is expected. [Microsoft's File Explorer guide](https://support.microsoft.com/en-us/windows/experience/fileexplorer/file-explorer-in-windows)

Register from the executable's permanent location. Register again if you move it or install the optional analyzer. Both registration and removal clear the previous all-files entry.

In the graphical workspace, choose **Explorer → Use this app in the context menu**. Both interfaces use the same **Inspect metadata with odmedia** entry. The last registration determines which executable Explorer opens. The CLI command `odmedia explorer remove` removes the shared entry.

## Installation

Download the Windows x64 executables from [Releases](https://github.com/odahan/OD.EditMediaInfo/releases) and keep them in the same folder. They share the `presets` subfolder and use the same optional `ffprobe.exe` and `ffmpeg.exe` installed beside them. You can move the entire folder to a different location. The `ODMEDIA_PRESET_DIR` and `ODMEDIA_FFPROBE` environment variables override the portable defaults.

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
odmedia info track.mp4 --export html --output report.html
odmedia info track.mp4 --export csv --output report.csv
odmedia info track.mp4 --export json --output report.json

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

* Presets are stored in `<exeDir>/presets/*.json` by default. Set `ODMEDIA_PRESET_DIR` to share a folder between copies of odmedia.
* For an editable audio or video file, open **F4 → Actions** and choose **Preset: create from this file**, **Preset: create from scratch**, or **Preset: stage for this file**. Staging adds pending edits that you can review and save. **Preset: manage** lists presets and lets you view their details or delete them.
* Generic fields only: `Artist, Album, AlbumArtist, Genre, Year, Composer, Comment, Copyright, Publisher, Cover(optional)` — `Title/Track/Disc` excluded by design. `EncodedBy` not available via TagLibSharp 2.3.0 and therefore not included.
* Cover is optional and limited to 10 MB; the interactive workflow warns when it exceeds 5 MB.

### `info` options

| Option | Description |
|--------|-------------|
| `--json` | Output raw JSON |
| `--export <json\|csv\|html>` | Write the full inspection report in the chosen format |
| `--output <path>` | Save the export to a new file. By default, export writes to standard output |
| `--analyze-cadence` | Inspect up to 300 frames from the first video track; explicitly sampled results |

Existing top-level JSON fields remain available, including the legacy audio-only `bitrate`. Extended results are additive under `details` (`Kind`, `Container`, `OverallBitrate` in bit/s, capability flags, source-labelled `Properties`, `Streams`, `Chapters` with seconds, `Diagnostics`, and the complete `ProbeData`). Track JSON retains the FFprobe field names and units.

The terminal’s **F4 → Actions** menu offers all three export formats. **Export complete JSON** and `--export json` use the same full schema as the GUI. CSV contains one row per property with **Section**, **Field**, **Value**, and **Source** columns. Text resembling a spreadsheet formula is prefixed with an apostrophe.

## Build

Double-click `Publish.BAT` in File Explorer or run `.\build.ps1` from PowerShell. One run publishes both self-contained Windows x64 executables, `odmedia.exe` and `odmedia-gui.exe`, to `Publish/` beside the scripts.

```powershell
dotnet build OD.EditMediaInfo.slnx -c Release -m:1
dotnet test OD.EditMediaInfo.slnx -m:1
.\build.ps1
.\Publish\odmedia.exe --help
.\Publish\odmedia-gui.exe
```

Prerequisites are .NET SDK 10.0.400 or a compatible newer SDK and Windows x64.

## Structure

- `src/OD.EditMediaInfo.Core` contains the shared inspection, metadata writing, preset services, and editing state.
- `src/OD.EditMediaInfo` contains CLI command parsing and the interactive Spectre.Console workspace.
- `src/OD.EditMediaInfo.Gui` contains the WPF workspace and its dialogs.
- `tests/OD.EditMediaInfo.Tests` exercises the shared services and CLI.
