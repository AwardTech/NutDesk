# IconGen

Draws every icon and picture NutDesk ships, so the set can be regenerated or restyled from code instead of being
edited bitmap by bitmap. It is a small .NET Framework 4.8 console program that renders with WPF
(`DrawingVisual` + `RenderTargetBitmap`) and writes the `.ico` containers itself. It needs nothing beyond the
.NET Framework and the Visual Studio Build Tools already used for NutDesk.

## Build

```
"C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe" tools\IconGen\IconGen.csproj -p:Configuration=Release -p:OutDir=bin\Release\
```

The project is not part of `NutDesk.sln`, and its `bin\` and `obj\` folders are not committed.

## Run

From the repository root:

```
tools\IconGen\bin\Release\IconGen.exe . [--sheet <path>\icon_sheet.png]
```

The argument is the repository root. The program overwrites:

| Output | What |
| --- | --- |
| `src\NutDesk\Resources\1025.ico` ... `1344.ico` | the 24 status icons (tray and main window) |
| `src\NutDesk\Resources\NutDesk.ico` | application icon (exe, notifications, dialogs) |
| `src\NutDesk\Resources\NutDesk-Updater.ico` | application icon with a download badge |
| `src\NutDesk\Resources\2001_ViewLogFile.ico`, `2002_Delete_LogFile.ico` | log file icons |
| `src\NutDesk\Resources\*.png` | pictures of the WinForms dialogs and menus, each at its original pixel size |
| `assets\logo.png`, `assets\logo-512.png` | logo for the README |
| `assets\social-preview.png` | 1280 x 640 GitHub social preview |

With `--sheet`, it also writes review sheets next to the given path: `icon_sheet.png` (every status icon at every
size, light and dark), `icon_sheet_zoom_battery.png` and `icon_sheet_zoom_mains.png` (tray sizes at 4x, pixel for
pixel) and `icon_sheet_brand.png` (application icons and dialog pictures). Look at them after any change: the tray
sizes must stay sharp and every state must be told apart at 16 px.

The file names and resource names do not change, so nothing else has to be touched after a run; rebuild NutDesk to
embed the new images.

## Status icons

The file name is the value MainForm asks for: `IDX_OFFSET` (1024) plus the `AppIconIdx` flags.

| Flag | Value | Drawing |
| --- | --- | --- |
| `IDX_BATT_0` ... `IDX_BATT_100` | 1, 2, 4, 8, 16 | battery fill level |
| `IDX_OL` | 32 | on mains: green bolt beside the battery, fill in the glyph colour |
| (no `IDX_OL`) | | on battery: 25 % fill amber, 0 % red |
| `WIN_DARK` | 64 | variant for a dark background: light glyph |
| `IDX_ICO_OFFLINE` | 128 | dimmed empty battery, struck through |
| `IDX_ICO_RETRY` | 256 | clockwise arrow in the accent colour (reconnecting) |

Every icon has 16, 20, 24, 32, 40, 48, 64 and 256 px frames. 16 to 32 px are laid out by hand on the pixel grid
(`StatusArt.For`): whole-pixel edges, one or two pixel strokes centred so they do not straddle pixels. The larger
frames scale the 32 px layout and use the slanted bolt of the logo.

Glyph colours follow the app: `#1B1B1B` on light, `#F2F2F2` on dark; amber, red, green and the accent are the app's
warning, danger, success and accent colours, adjusted where a few pixels of fill needed more contrast.

## Application icon

A flat-topped hex nut with a lightning bolt, white on a rounded tile shading from deep blue through the light theme
accent (`#0F6CBD`) to teal. Up to 32 px the nut is solid with the bolt cut out of it; from 40 px it gets its round
hole and the bolt passes through. Frames: 16, 20, 24, 32, 40, 48, 64, 96, 128 and 256 px.

## .ico format

`ICONDIR` + one `ICONDIRENTRY` per frame, largest first. Frames below 256 px are 32-bit DIBs (BGRA plus an AND mask
of the fully transparent pixels), which Explorer, the notification area and GDI+ all read; the 256 px frame is PNG.
