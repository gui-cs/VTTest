# VTTest

A cross-platform console application for exploring raw ANSI/VT escape sequences and investigating how stdin behaves differently across Windows and Unix platforms.

## Why This Exists

Terminal input is surprisingly inconsistent across platforms. On Windows, `ReadFile` and `Console.OpenStandardInput` behave differently (e.g., Ctrl+Z triggers an EOF on streams but not on `ReadFile`). On Unix, .NET's `Console.OpenStandardInput()` adds buffering that requires a newline before returning data, even with `stty raw` — so VTTest reads fd 0 directly to get true raw input. This tool makes all of that visible.

## What It Does

VTTest puts the console into raw/VT input mode and reads input directly. Every keypress, mouse click, or mouse move is displayed as:

- A **human-readable interpretation** (e.g., `[Ctrl+A]`, `[Up]`, `[Alt+X]`, `[Mouse Left press @10,5]`)
- The **raw hex bytes** of the escape sequence

### Read Modes

VTTest supports three input-reading strategies, each in its own file with independent logic. Press `s` to cycle between them at runtime.

| Mode | Label | File | Description |
|------|-------|------|-------------|
| **Native ReadFile** | `RdFl` | `NativeInputReader.cs` | Win32 `ReadFile` P/Invoke. Windows only. |
| **Stream** | `Strm` | `StreamInputReader.cs` | Synchronous `Stream.Read` — `Console.OpenStandardInput()` on Windows, raw fd 0 `FileStream` on Unix. |
| **Async Stream** | `Async` | `AsyncStreamInputReader.cs` | Background `Task` calling `Stream.ReadAsync` in a loop, buffering into a `BlockingCollection`. Consumer blocks on `Take()`. Demonstrates the pattern used by Terminal.Gui's `WindowsVTInputHelper`. |

**Cycling order:**
- Windows: Native → Stream → Async → Native → …
- Unix: Stream → Async → Stream → …

### Platform-Specific Behavior

**Windows:**
- Uses Win32 Console API via P/Invoke (`ReadFile`), .NET streams (`Console.OpenStandardInput`), or async streams
- Cycle between read modes with `s` to compare behavior (e.g., Ctrl+Z returns 0 bytes on streams but 0x1A via `ReadFile`)
- Console mode flags (`ENABLE_VIRTUAL_TERMINAL_INPUT`, `ENABLE_PROCESSED_INPUT`) control how input is delivered

**Unix (macOS/Linux/WSL):**
- Uses `stty raw -echo -icanon` for raw terminal mode
- Reads stdin fd 0 directly (not `Console.OpenStandardInput()`, which buffers line-by-line even in raw mode)
- Signal handling (`isig`) can be toggled at runtime to test Ctrl+Z suspend/resume and Ctrl+C behavior
- SIGCONT handler restores raw mode and redraws UI after `fg` resume
- SIGINT handler performs terminal cleanup (reset scroll region, disable mouse tracking, restore stty) before exiting

## Supported Input

- **Keyboard**: Arrow keys, Home/End, Page Up/Down, F1-F12, Tab, Enter, Backspace, Escape
- **Modifiers**: Ctrl, Alt, Shift, and combinations thereof
- **Mouse**: Left/Middle/Right click, scroll wheel, motion tracking (SGR encoding)
- **Control characters**: Ctrl+A through Ctrl+Z and others

## Controls

| Key | Action |
|-----|--------|
| `q` | Quit |
| `s` | Cycle read mode: Native → Stream → Async (Windows) or Stream → Async (Unix) |
| `z` | Toggle signal handling — when ON, Ctrl+Z suspends and Ctrl+C exits cleanly; when OFF, they appear as raw input |
| `c` | Clear the output area |

## Architecture

```
Program.cs                  – Main loop, console setup, signal handlers, reader cycling
IInputReader.cs             – Interface: Label, DisplayName, Read(byte[])
NativeInputReader.cs        – Win32 ReadFile P/Invoke (Windows only)
StreamInputReader.cs        – Synchronous Stream.Read
AsyncStreamInputReader.cs   – Background ReadAsync + BlockingCollection
AnsiSequenceParser.cs       – Parses raw bytes into human-readable descriptions
TerminalUI.cs               – VT output helpers (header, scroll region, mouse tracking)
NativeConsole.cs            – Win32 Console P/Invoke declarations
```

Each `IInputReader` implementation is fully self-contained — changes to one have no impact on the others.

## Requirements

- .NET 10
- Windows (Win32 Console API via P/Invoke) or Unix-like (macOS, Linux, WSL via `stty`)

## Building and Running

```
dotnet build
dotnet run
```
