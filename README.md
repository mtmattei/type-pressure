# Type Pressure — Uno desktop

A native Uno Platform / XAML typography experiment. The exhibition canvas is drawn with Skia, using the original OpenType variation data from Roboto Flex and Anybody.

## Run

Install the .NET 10 SDK, then open `Launch Type Pressure.cmd` on Windows. The launcher builds the app on first use and opens the desktop preview. Build output is generated locally and is not checked into the repository.

For development, open `TypePressure.sln`, or run these commands from the `TypePressure` project folder with .NET 10 installed:

```powershell
dotnet build -f net10.0-desktop
dotnet run -f net10.0-desktop
```

## Play

- Hold anywhere in the chamber to inflate the nearby letters. Drag to add a slant.
- Drag the side handles to compress the chamber.
- Add up to six force fields: inflate, compress, lean, or oscillate. Drag their orange markers.
- Choose Breathing room, Tug of war, Aftershock, or Specimen storm.
- Change the text, typeface, and the three base axes. Anybody has no slant axis, so that control is disabled for this font.
- Save a specimen as a 2× PNG or an SVG containing vector letter outlines.

| Key | Action |
| --- | --- |
| D | Enter the full-screen, text-only demo |
| Escape | Exit demo / exit fullscreen |
| Double-click the demo | Exit demo |
| Space | Freeze / resume |
| R | Reset the current chamber |
| F11 | Toggle fullscreen |
| F | Select the next force field |
| Arrow keys | Move the selected force field |
| Delete | Remove the selected force field |

Demo mode loops through seven eight-second sequences: weight, width, slant, traveling pressure, opposing forces, aftershocks, and Specimen Storm. Portrait windows use five stacked rows, expanding to nine rows for the storm. It uses your current text and font, hides every control, and returns to your paused/running state when you exit. The brief exit hint disappears automatically.

## Portrait video

### Live Uno window recording

Open `Launch Portrait Demo.cmd` to launch the real desktop app in a borderless 630 × 1120 window. It starts with a still frame; press Space to start (or restart) the full seven-scene loop, including Specimen Storm. All controls, tooltips, hints, and developer overlays stay hidden. Escape or double-click still exits demo mode.

The launch option is `--portrait-demo`. Capture the window titled `Type Pressure — Portrait Demo` with your screen recorder, with cursor capture disabled. This records the running Uno application. The app logs scene transitions to standard output when started from a terminal.

### Offline renderer

`tools/VideoExport` renders the same `DemoRenderer` and `PressureSimulation` as the native app into a complete 56-second portrait sequence: 1080 × 1920, 30 fps, with PRESSURE as the material. No window chrome, cursor, exit hints, or development overlays are included.

From this folder, run `dotnet run --project tools/VideoExport -c Release -- <frames-directory>` to create the PNG sequence. Add `--preview` to render one sample from each scene. Encode the sequence with FFmpeg using `-framerate 30 -i frame-%05d.png -c:v libx264 -crf 18 -pix_fmt yuv420p -movflags +faststart demo.mp4`.

## Implementation

- `MainPage.xaml`: native controls, menus, responsive layout, and accessibility labels.
- `Rendering/PressureCanvas.cs`: drawing, pointer interactions, and PNG/SVG export.
- `Rendering/DemoRenderer.cs`: shared portrait/landscape demo drawing and video export.
- `Rendering/PressureSimulation.cs`: force math and the 56-second demo cycle.
- `Rendering/VariableFont.cs`: OpenType variation scalar evaluation, composite glyphs, quadratic outlines, and advance metrics.

Uno 6's SkiaSharp 3 package does not expose variable-font cloning. To retain real axis changes, `tools/prepare-fonts.py` extracts the fonts' gvar tuples with FontTools, resolves IUP interpolation, and pins the unused custom axes. The app evaluates weight, width, and slant continuously in C#; it does not horizontally stretch glyphs or use a browser. Roboto Flex's optical size is pinned at 144 for this display use.

Both font families and their licenses are bundled. Font data includes their complete character maps; characters outside a selected font's coverage display its missing-glyph symbol. Text is treated as individual glyphs for the experiment, rather than as a multilingual shaped paragraph.

## Validation

```powershell
dotnet bin/Debug/net10.0-desktop/TypePressure.dll --self-test
```

The regression check compares 48 native font instances, including accented composite glyphs, against independent FontTools instances. Maximum observed coordinate difference: 0.503 font units, within rounding tolerance. It also checks freeze behavior and two complete demo loops for valid axis ranges.

Windows desktop is the built and tested target. The project uses Uno's desktop host configuration for Windows, macOS, and Linux; macOS and Linux have not been run in this workspace.

Font sources: [Roboto Flex](https://github.com/google/fonts/tree/main/ofl/robotoflex), [Anybody](https://github.com/google/fonts/tree/main/ofl/anybody). Both use the SIL Open Font License; see `TypePressure/Assets/Fonts`.
