# VTTest

A cross-platform console application for exploring raw ANSI/VT escape sequences and investigating how stdin behaves differently across Windows and Unix platforms.

## Why This Exists

Terminal input is surprisingly inconsistent across platforms. On Windows, `ReadFile` and `Console.OpenStandardInput` behave differently (e.g., Ctrl+Z triggers an EOF on streams but not on `ReadFile`). On Unix, .NET's `Console.OpenStandardInput()` adds buffering that requires a newline before returning data, even with `stty raw` — so VTTest reads fd 0 directly to get true raw input. This tool makes all of that visible.

## What It Does

VTTest puts the console into raw/VT input mode and reads input directly. Every keypress, mouse click, or mouse move is displayed as:

- A **human-readable interpretation** (e.g., `[Ctrl+A]`, `[Up]`, `[Alt+X]`, `[Mouse Left press @10,5]`)
- The **raw hex bytes** of the escape sequence

### Platform-Specific Behavior

**Windows:**
- Uses Win32 Console API via P/Invoke (`ReadFile`) or .NET streams (`Console.OpenStandardInput`)
- Toggle between read methods with `s` to compare behavior (e.g., Ctrl+Z returns 0 bytes on streams but 0x1A via `ReadFile`)
- Console mode flags (`ENABLE_VIRTUAL_TERMINAL_INPUT`, `ENABLE_PROCESSED_INPUT`) control how input is delivered

**Unix (macOS/Linux):**
- Uses `stty raw -echo -icanon` for raw terminal mode
- Reads stdin fd 0 directly (not `Console.OpenStandardInput()`, which buffers line-by-line even in raw mode)
- Signal handling (`isig`) can be toggled at runtime to test Ctrl+Z suspend/resume and Ctrl+C behavior
- SIGCONT handler restores raw mode and redraws UI after `fg` resume

## Supported Input

- **Keyboard**: Arrow keys, Home/End, Page Up/Down, F1-F12, Tab, Enter, Backspace, Escape
- **Modifiers**: Ctrl, Alt, Shift, and combinations thereof
- **Mouse**: Left/Middle/Right click, scroll wheel, motion tracking (SGR encoding)
- **Control characters**: Ctrl+A through Ctrl+Z and others

## Controls

| Key | Action |
|-----|--------|
| `q` | Quit |
| `s` | Toggle between `ReadFile` (P/Invoke) and `Stream` read modes (Windows only) |
| `z` | Toggle signal handling — when ON, Ctrl+Z suspends and Ctrl+C exits; when OFF, they appear as raw input |
| `c` | Clear the output area |

## Requirements

- .NET 10
- Windows (Win32 Console API via P/Invoke) or Unix-like (macOS, Linux via `stty`)

## Building and Running

```
dotnet build
dotnet run
```
