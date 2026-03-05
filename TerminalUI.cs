using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace VTTest;

/// <summary>
/// Display, output, and layout helpers for the VT test terminal UI.
/// </summary>
internal static class TerminalUI
{
    private static Stream? s_stdoutStream;

    internal static void Write(IntPtr hOut, string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            NativeConsole.WriteFile(hOut, bytes, (uint)bytes.Length, out _, IntPtr.Zero);
        }
        else
        {
            s_stdoutStream ??= Console.OpenStandardOutput();
            s_stdoutStream.Write(bytes, 0, bytes.Length);
            s_stdoutStream.Flush();
        }
    }

    internal static void DisposeStdoutStream()
    {
        s_stdoutStream?.Dispose();
        s_stdoutStream = null;
    }

    /// <summary>
    /// Draws the centered instruction box at the top of the screen. Returns the row after the box.
    /// </summary>
    internal static int DrawHeader(IntPtr hOut, int width, bool isWindows)
    {
        int boxWidth = Math.Min(80, width - 2);
        int leftMargin = (width - boxWidth) / 2;

        Write(hOut, $"\x1b[1;{leftMargin}H");
        Write(hOut, "┌" + new string('─', boxWidth - 2) + "┐");

        string[] instructions = isWindows
            ? [
                "VT Input Test - Raw ANSI Escape Sequences",
                "",
                "Try: Arrow keys, Function keys, Mouse, Ctrl+Z",
                "'s' = toggle Stream/ReadFile, 'c' = clear, 'q' = quit"
            ]
            : [
                "VT Input Test - Raw ANSI Escape Sequences",
                "",
                "Try: Arrow keys, Function keys, Mouse",
                "'c' = clear, 'q' = quit"
            ];

        for (var i = 0; i < instructions.Length; i++)
        {
            Write(hOut, $"\x1b[{2 + i};{leftMargin}H");
            var line = instructions[i];
            int padding = (boxWidth - 2 - line.Length) / 2;

            Write(hOut,
                "│" + new string(' ', padding) + line +
                new string(' ', boxWidth - 2 - padding - line.Length) + "│");
        }

        Write(hOut, $"\x1b[{2 + instructions.Length};{leftMargin}H");
        Write(hOut, "└" + new string('─', boxWidth - 2) + "┘");

        return 2 + instructions.Length + 1;
    }

    /// <summary>
    /// Draws the console mode status line at the given row.
    /// </summary>
    internal static void DrawStatusLine(IntPtr hOut, int row, uint mode, bool isWindows)
    {
        Write(hOut, $"\x1b[{row};1H");

        if (isWindows)
        {
            Write(hOut,
                $"Mode: 0x{mode:X} | VT:{((mode & NativeConsole.ENABLE_VIRTUAL_TERMINAL_INPUT) != 0 ? "ON" : "OFF")} Mouse:{((mode & NativeConsole.ENABLE_MOUSE_INPUT) != 0 ? "ON" : "OFF")}");
        }
        else
        {
            Write(hOut, "Mode: Unix raw | VT:ON Mouse:ON");
        }
    }

    /// <summary>
    /// Updates the read-method label in the fixed header area.
    /// </summary>
    internal static void UpdateMethodLine(IntPtr hOut, int row, bool useStream)
    {
        var methodName = useStream ? "Stream (Console.OpenStandardInput)" : "ReadFile (P/Invoke)";
        Write(hOut, $"\x1b[s\x1b[{row};1H\x1b[2KRead: {methodName}\x1b[u");
    }

    /// <summary>
    /// Writes the initial read-method label.
    /// </summary>
    internal static void DrawMethodLine(IntPtr hOut, int row, bool useStream)
    {
        Write(hOut, $"\x1b[{row};1H");
        Write(hOut, useStream ? "Read: Stream (Console.OpenStandardInput)" : "Read: ReadFile (P/Invoke)");
    }

    internal static void SetScrollRegion(IntPtr hOut, int top, int bottom)
    {
        Write(hOut, $"\x1b[{top};{bottom}r");
        Write(hOut, $"\x1b[{top};1H");
    }

    internal static void ResetScrollRegion(IntPtr hOut) => Write(hOut, "\x1b[r");

    internal static void ClearScrollRegion(IntPtr hOut, int scrollTop) => Write(hOut, $"\x1b[{scrollTop};1H\x1b[J");

    internal static void EnableMouseTracking(IntPtr hOut) => Write(hOut, "\x1b[?1003h\x1b[?1006h");

    internal static void DisableMouseTracking(IntPtr hOut) => Write(hOut, "\x1b[?1003l\x1b[?1006l");

    internal static void ClearScreen(IntPtr hOut) => Write(hOut, "\x1b[2J\x1b[H");

    internal static (int width, int height) GetConsoleSize(IntPtr hOut, bool isWindows)
    {
        if (isWindows)
        {
            NativeConsole.GetConsoleScreenBufferInfo(hOut, out var csbi);
            int w = csbi.srWindow.Right - csbi.srWindow.Left + 1;
            int h = csbi.srWindow.Bottom - csbi.srWindow.Top + 1;
            return (w, h);
        }

        int width = Console.WindowWidth > 0 ? Console.WindowWidth : 80;
        int height = Console.WindowHeight > 0 ? Console.WindowHeight : 24;
        return (width, height);
    }

    internal static string? RunProcess(string fileName, string arguments)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);

            if (proc == null)
                return null;

            var output = proc.StandardOutput.ReadToEnd().Trim();
            proc.WaitForExit();
            return output;
        }
        catch
        {
            return null;
        }
    }
}
