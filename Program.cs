using System.Runtime.InteropServices;
using System.Text;

namespace VTTest;

internal class Program
{
    private static readonly bool s_isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    private static void Main()
    {
        var hIn = IntPtr.Zero;
        var hOut = IntPtr.Zero;
        uint originalMode = 0;
        uint mode = 0;
        string? originalSttyState = null;

        if (s_isWindows)
        {
            hIn = NativeConsole.GetStdHandle(NativeConsole.STD_INPUT_HANDLE);
            hOut = NativeConsole.GetStdHandle(NativeConsole.STD_OUTPUT_HANDLE);

            if (hIn == IntPtr.Zero || hOut == IntPtr.Zero)
            {
                TerminalUI.Write(hOut, "Failed to get handles\r\n");
                return;
            }

            if (!NativeConsole.GetConsoleMode(hIn, out originalMode))
            {
                TerminalUI.Write(hOut, "Failed to get console mode\r\n");
                return;
            }

            // Enable virtual terminal input and disable the processed modes
            mode = originalMode;
            mode |= NativeConsole.ENABLE_VIRTUAL_TERMINAL_INPUT | NativeConsole.ENABLE_MOUSE_INPUT;
            mode &= ~(NativeConsole.ENABLE_PROCESSED_INPUT | NativeConsole.ENABLE_LINE_INPUT |
                       NativeConsole.ENABLE_ECHO_INPUT | NativeConsole.ENABLE_QUICK_EDIT_MODE);

            if (!NativeConsole.SetConsoleMode(hIn, mode))
            {
                TerminalUI.Write(hOut, "Failed to set console mode\r\n");
                return;
            }
        }
        else
        {
            // Unix: save stty state and set raw mode
            originalSttyState = TerminalUI.RunProcess("stty", "-g");
            TerminalUI.RunProcess("stty", "raw -echo -icanon -isig min 1");
        }

        var (width, height) = TerminalUI.GetConsoleSize(hOut, s_isWindows);

        TerminalUI.EnableMouseTracking(hOut);
        TerminalUI.ClearScreen(hOut);

        // Draw header box and status lines
        int statusRow = TerminalUI.DrawHeader(hOut, width, s_isWindows);
        TerminalUI.DrawStatusLine(hOut, statusRow, mode, s_isWindows);

        var useStream = !s_isWindows;
        Stream? stdinStream = null;

        int methodRow = statusRow + 1;
        TerminalUI.DrawMethodLine(hOut, methodRow, useStream);

        // Set scroll region to rows below the header, leaving header fixed
        int scrollTop = methodRow + 2;
        TerminalUI.SetScrollRegion(hOut, scrollTop, height);

        var buffer = new byte[256];

        while (true)
        {
            int bytesRead;

            if (useStream)
            {
                stdinStream ??= Console.OpenStandardInput();
                bytesRead = stdinStream.Read(buffer, 0, buffer.Length);

                if (bytesRead == 0)
                {
                    TerminalUI.Write(hOut, "[Ctrl+Z] (Stream returned 0 bytes - Windows EOF bug)\r\n");
                    continue;
                }
            }
            else
            {
                if (!NativeConsole.ReadFile(hIn, buffer, (uint)buffer.Length, out uint nativeRead, IntPtr.Zero))
                    break;

                bytesRead = (int)nativeRead;

                if (bytesRead == 0)
                {
                    TerminalUI.Write(hOut, "[Ctrl+Z] (ReadFile returned 0 bytes - Windows EOF bug)\r\n");
                    continue;
                }
            }

            var label = useStream ? "Strm" : "RdFl";
            var pretty = AnsiSequenceParser.FormatInput(buffer, bytesRead);
            var hex = AnsiSequenceParser.FormatRawHex(buffer, bytesRead);
            TerminalUI.Write(hOut, $"{label}: {pretty}  \x1b[90m({hex})\x1b[0m\r\n");

            var chunk = Encoding.UTF8.GetString(buffer, 0, bytesRead);

            if (chunk.Contains('q'))
                break;

            if (chunk.Contains('s') && s_isWindows)
            {
                useStream = !useStream;

                if (!useStream)
                {
                    stdinStream?.Dispose();
                    stdinStream = null;
                }

                TerminalUI.UpdateMethodLine(hOut, methodRow, useStream);
            }

            if (chunk.Contains('c'))
                TerminalUI.ClearScrollRegion(hOut, scrollTop);
        }

        stdinStream?.Dispose();

        // Cleanup
        TerminalUI.ResetScrollRegion(hOut);
        TerminalUI.DisableMouseTracking(hOut);

        if (s_isWindows)
        {
            NativeConsole.SetConsoleMode(hIn, originalMode);
        }
        else if (originalSttyState != null)
        {
            TerminalUI.RunProcess("stty", originalSttyState);
        }

        TerminalUI.Write(hOut, $"\x1b[{height};1H\r\nRestored console mode.\r\n");
        TerminalUI.DisposeStdoutStream();
    }
}
