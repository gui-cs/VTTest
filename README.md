# VTTest

A cross-platform console application that displays raw ANSI/VT escape sequences as you type. Useful for understanding exactly what byte sequences your terminal sends for keyboard and mouse input.

## What It Does

VTTest puts the console into raw/VT input mode and reads input directly. On Windows it uses `ReadFile` (P/Invoke) or `Console.OpenStandardInput` (togglable); on Unix-like platforms it uses `stty raw` and streams. Every keypress, mouse click, or mouse move is displayed as:

- A **human-readable interpretation** (e.g., `[Ctrl+A]`, `[Up]`, `[Alt+X]`, `[Mouse Left press @10,5]`)
- The **raw hex bytes** of the escape sequence

This makes it easy to see how your terminal encodes keys, modifiers, function keys, mouse events, and more.

## Supported Input

- **Keyboard**: Arrow keys, Home/End, Page Up/Down, F1-F12, Tab, Enter, Backspace, Escape
- **Modifiers**: Ctrl, Alt, Shift, and combinations thereof
- **Mouse**: Left/Middle/Right click, scroll wheel, motion tracking (SGR encoding)
- **Control characters**: Ctrl+A through Ctrl+Z and others

## Controls

| Key | Action |
|-----|--------|
| `q` | Quit |
| `s` | Toggle between `ReadFile` (P/Invoke) and `Stream` (Console.OpenStandardInput) read modes (Windows only) |
| `c` | Clear the output area |

## Requirements

- .NET 10
- Windows (Win32 Console API via P/Invoke) or Unix-like (macOS, Linux via `stty`)

## Building and Running

```
dotnet build
dotnet run
```
