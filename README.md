# VTTest

A Windows console application that displays raw ANSI/VT escape sequences as you type. Useful for understanding exactly what byte sequences your terminal sends for keyboard and mouse input.

## What It Does

VTTest puts the Windows console into **Virtual Terminal Input** mode and reads raw input using either `ReadFile` (P/Invoke) or `Console.OpenStandardInput`. Every keypress, mouse click, or mouse move is displayed as:

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
| `s` | Toggle between `ReadFile` (P/Invoke) and `Stream` (Console.OpenStandardInput) read modes |
| `c` | Clear the output area |

## Requirements

- Windows (uses Win32 Console API via P/Invoke)
- .NET 10

## Building and Running

```
dotnet build
dotnet run
```
