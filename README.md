# DotNetCommander

DotNetCommander is a two-pane file manager in the style of Total Commander / Norton Commander / Double Commander on Windows Forms and .NET 8.

## About the Project

A functional Windows-first file manager with its own Quick View, built-in editors, asynchronous file operations, and commander-style hotkeys.

Current active build:
- `Commander.NET.csproj`
- `net8.0-windows10.0.22621.0`
- version `1.9.0`

## Key Features

- Local AI folder organizer (`Tools -> AI folder organizer...`): processes files with configurable packages, requires OpenAI API key for each operation;
- Two file panels with address bar, disk panel and commander-style navigation;
- Search `Alt+F7` by mask/regex and content in background; results open directly in active panel, API tags, completions and capabilities accessible via `copilot move/skip/need_content`, persistent console for commands, background mode for API, and capability filtering;
- Column sorting with persistent first `..` and optional `Options -> View` mode where directories and files sort together and read-only/hidden files also sort together;
- Quick View (`F2` / `Ctrl+Q`) for text, Markdown, images, RTF and small CSV-like files;
- Built-in file compare via `Shift+F3`: text diff, CSV diff, image compare and binary summary;
- Streaming RAW/Hex viewer for viewing signatures and arbitrary offsets without loading the entire file; `F3` is fallback for binary, XML declaration `encoding="windows-1251"` encoding detection for XML-based text files like `.fb2`; BOM takes priority, read-only DataSet panels with directory tables and relations, arbitrary columns and row preview;
- Open CFBF/OLE2 containers (`.doc`, `.xls`, `.ppt`, `.msg`, `.msi` and others) via `Ctrl+PgDn` as read-only hierarchy storages/streams with Quick View, RAW and copy-out; Word Binary `F3` Quick View main document for plain text fallback; Declaration `<?xml` encoding detection for XML-based text files like `.fb2`; BOM takes priority, read-only DataSet panels with directory tables and relations, arbitrary columns and row preview;
- Open `.ged` as read-only GEDCOM directory with sections persons (`INDI`), families (`FAM`), notes (`NOTE`) and other tags of zero depth; Quick View shows full text of selected record, `F3` Quick View main document for plain text fallback; Declaration `<?xml` encoding detection for XML-based text files like `.fb2`; BOM takes priority, read-only DataSet panels with directory tables and relations, arbitrary columns and row preview;
- Shared system icon cache by extension/type, so directories and files have the same icons in the file panel and other UI;
- Commander-style command line below panels for running programs, scripts and shell commands with parameters in panel directory;
- Shell integration: `Open`, `Open with...`, `Properties`;
- `Shift+Enter` to run `.bat/.cmd/.exe/.com/.ps1` or entered command in a persistent console;
- `Ctrl+R` to manually refresh the active file panel;
- `Ctrl+N` to create a new file via `SaveFileDialog`;
- `Shift+F4` for commander-style create new file in active panel;
- `Shift+F5` for copy with new name in same panel;
- `Shift+F6` for inline rename;
- `F7` for commander-style create directory, including nested paths, with option to immediately go to new folder;
- Live bottom F-panel with separate `Shift` / `Alt` / `Ctrl` indicator, empty unsupported combinations and visible `Ctrl+F3` RAW/Hex;
- Customizable fonts for standard dialogs with separate roles `header/body/emphasis/caption` and live preview in `Options`;
- Interface language settings with `Auto` mode or explicit choice `EN/DE/UK`;
- Optional auto-refresh panel on external changes in open directory;
- Save window position, size and maximized state with check against current monitor configuration;
- Localization `EN`, `DE`, `UK`;
- Centralized error logging alongside user config.

## Key Components

Core project parts:

- `Application/` — main window and commander command coordination;
- `Browsers/` — base contract, physical file panel, archive-, GEDCOM- and DataSet-providers;
- `Controls/` — breadcrumb navigation and Quick View controls;
- `Dialogs/Common/`, `Dialogs/Operations/`, `Dialogs/Legacy/` — service, operational and compatible legacy dialogs;
- `Editors/Text/`, `Editors/RichText/` — built-in text and RTF editors along with their resources;
- `Viewers/Csv/`, `Viewers/Compare/`, `Viewers/Images/` — CSV viewer, compare and image/EXIF viewer with dependent files;
- `Services/` — archives, file classification, icons, operations and shared UI style;
- `ThirdParty/OpenMcdf/` — locally pinned source snapshot CFBF library, compiles directly into `DotNetCommander.dll` as NuGet DLL or Word Binary `F3` Quick View main document for plain text fallback;
- `Infrastructure/` — logging, performance trace, localization, settings and platform code.

## Keyboard Shortcuts

- `F2` / `Ctrl+Q` — Quick View
- `F3` — view
- `Ctrl+F3` — force RAW/Hex view
- `Shift+F3` — compare with counterpart in passive panel
- `F4` — edit
- `Ctrl+R` — refresh active panel
- `Shift+F4` — create new file in active panel
- `Ctrl+N` — create new file via system dialog
- `F5` — copy to passive panel
- In open archive `F5` — extract selected entries to passive file panel
- In GEDCOM panel `F3` — show full text of selected record, `F5` — export all selected records to passive panel
- `Ctrl+PgDn` — open `.ged`, archive/CFBF signature or try to read selected file as DataSet XML regardless of extension
- `Backspace` — return to previous panel list, including moving back to another drive or archive
- `Ctrl+PgUp` — go strictly to parent directory and select directory from which return was made
- `Shift+F5` — create copy in same panel
- `Alt+F5` — pack selected items into archive
- `F6` — move to passive panel
- `Shift+F6` — rename
- `F7` — new folder
- `F8` — delete
- `Alt+F9` — extract selected archive
- `Ctrl+L` — go to command line
- `Ctrl+Enter` — insert element name under cursor into command line
- `Enter` in command line — execute command in active panel directory
- `Up` / `Down` in command line — view command history
- `Shift+Enter` — run selected `.bat/.cmd/.exe/.com/.ps1` or entered command in persistent console

## Build

### Requirements
- Windows
- .NET SDK 8+

### Command
- `dotnet build Commander.NET.csproj`

Active build targets `AnyCPU/x64` and no longer depends on `x86-only` `IWshRuntimeLibrary`.

## Documentation

- [`CHANGE.md`](DotNetCMD/CHANGE.md) — change history
- [`ROADMAP.md`](DotNetCMD/ROADMAP.md) — current development plan
- [`AGENTS.md`](DotNetCMD/AGENTS.md) — current code technical state and working rules
- [`COMMENTS.md`](DotNetCMD/COMMENTS.md) — architectural explanations and fixed technical guidelines
