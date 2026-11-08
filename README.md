# Glint Widgets

Lightweight, high-performance desktop productivity widgets for Windows 10 & 11, engineered with pure native C# and a Liquid Glass design system.

Glint Widgets run directly on your desktop layer as hardware-accelerated layered windows with 1:1 wallpaper refraction, instant responsiveness, and zero browser overhead.

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![Platform: Windows 10 / 11](https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011-0078D6.svg)](#requirements)
[![Release](https://img.shields.io/github/v/release/Naveen-io/glint-widgets?color=success)](https://github.com/Naveen-io/glint-widgets/releases/latest)

---

## Highlights

* **Liquid Glass Material**: 1:1 crisp desktop wallpaper refraction with soft optical blur, Snell's law specular rim highlights, and 75% default glass opacity. No horizontal banding or artificial scanlines.
* **Pure Native Windows Engine**: 100% C# on .NET Framework 4 (pre-installed on every Windows 10/11 system). Zero Electron, zero WebView, zero runtime downloads.
* **Ultralight Resource Footprint**: All widgets run within a single shared background process consuming ~30–40 MB of RAM and ~0.2% CPU.
* **Instant Interaction**: Silky-smooth window dragging, native hit-testing, and persistent positioning across multi-monitor setups.
* **Encapsulated Photo Display**: Drop any picture or folder directly onto the Photo Frame widget—photos are displayed encapsulated under the curved glass sheen.
* **Exact Math Calculator**: Expression evaluation with operator precedence, parentheses, and real-time liquid glass key wells.

---

## Widget Catalog

| Widget | Description |
| :--- | :--- |
| **Photo Frame** | Showcase single photos (`.png`, `.jpg`, `.webp`, `.bmp`, `.gif`) or rotating folders. Supports Explorer drag-and-drop and liquid glass encapsulation. |
| **Calculator** | Minimalist desktop calculator with parentheses, operator precedence, percentage, and instant evaluation. |
| **System Monitor** | Live CPU usage, RAM utilization, and system hardware load meters. |
| **Focus Timer** | Clean Pomodoro and interval timer to maintain productivity bursts. |
| **Quick Notes** | Scratchpad with auto-save, Markdown formatting support, and drag-to-move capabilities. |
| **Quick Launcher** | Pinned application and web shortcuts for rapid desktop launching. |
| **Clipboard History** | Persistent clipboard history with one-click copy and paste preview. |
| **Weather** | Real-time temperature, meteorological conditions, and upcoming forecast for your location. |
| **Analog Clock** | Fluid analog desktop clock with smooth continuous second hand motion. |
| **Calendar & Now Playing**| Live schedule glance and Windows Media Session playback controls. |
| **Liquid Glass Test** | Real-time diagnostic widget demonstrating refraction, specular rim highlights, and glass controls. |

---

## Installation

### Option 1: Standalone Portable Binary (Recommended)
1. Download **`GlintWidgets.exe`** from the [Latest Release](https://github.com/Naveen-io/glint-widgets/releases/latest).
2. Move `GlintWidgets.exe` to your desired directory (e.g. `C:\Tools\GlintWidgets`).
3. Run `GlintWidgets.exe`.
   * Glint Widgets automatically creates a Start Menu entry and a System Tray icon.
   * Right-click any widget or the tray icon to customize themes, opacity, scaling, or open the Widget Library.

### Option 2: Build from Source
Compile directly using the Microsoft .NET C# compiler included with Windows:

```powershell
git clone https://github.com/Naveen-io/glint-widgets.git
cd glint-widgets
.\build.bat
```

The compiled binary will be placed at `bin\GlintWidgets.exe`.

---

## Architecture & Technology Stack

* **Language**: C# (.NET Framework 4.0 / 4.8 compatible)
* **Window Subsystem**: Win32 Layered Windows (`WS_EX_LAYERED`, `UpdateLayeredWindow`, GDI+)
* **Backdrop Engine**: Native desktop wallpaper sampler at 1:1 virtual screen bounds with memory-mapped 32bpp PARGB separable soft blur.
* **Persistence**: INI-based configuration (`%APPDATA%\DeskWidgets\widgets.ini`) storing monitor coordinates, custom dimensions, opacity, and widget parameters.
* **Dependencies**: None. Uses only core Windows system libraries (`System.Windows.Forms`, `System.Drawing`, `Windows.Media.winmd`).

---

## Requirements

* **Operating System**: Windows 10 (version 1809 or higher) or Windows 11 (all editions, x64).
* **Runtime**: .NET Framework 4.8 (comes preinstalled on Windows 10 and 11).

---

## Credits & Attribution

GLINT Widgets is built upon and inspired by the excellent open-source [Desk Widgets](https://github.com/whoissaaif/desk-widgets) project created by **whoissaaif** and contributors.

Glint Widgets extends the project with:
* Full Liquid Glass design language inspired by `@zakisheriff/liquid-glass`.
* 1:1 native-resolution desktop wallpaper refraction with soft optical blur.
* Single-image file selection and drag-and-drop photo encapsulation.
* High-precision expression evaluator for the calculator widget.
* Custom brand identity, modern icon assets, and refreshed management windows.

---

## License

This project is licensed under the [MIT License](LICENSE).  
Copyright (c) 2026 Naveen-io and GLINT Widgets contributors.  
Copyright (c) 2026 whoissaaif and Desk Widgets contributors.
