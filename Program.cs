using System.Runtime.InteropServices;
using System.Text;

namespace VTTest;

internal class Program
{
    private static readonly bool s_isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    // Shared state for signal handlers
    private static IntPtr s_hOut;
    private static IntPtr s_hIn;
    private static int s_scrollTop;
    private static int s_height;
    private static int s_width;
    private static uint s_mode;
    private static bool s_signalMode;
    private static int s_statusRow;
    private static int s_methodRow;
    private static IInputReader s_reader = null!;
    private static string? s_originalSttyState;
    private static uint s_originalMode;

    private static void Main()
    {
        s_hIn = IntPtr.Zero;
        s_hOut = IntPtr.Zero;
        s_mode = 0;
        s_originalMode = 0;
        s_originalSttyState = null;

        if (s_isWindows)
        {
            // Ensure console uses UTF-8 so box-drawing characters render correctly
            // (VS Code debugger may launch with the system code page instead)
            NativeConsole.SetConsoleOutputCP(NativeConsole.CP_UTF8);

            s_hIn = NativeConsole.GetStdHandle(NativeConsole.STD_INPUT_HANDLE);
            s_hOut = NativeConsole.GetStdHandle(NativeConsole.STD_OUTPUT_HANDLE);

            if (s_hIn == IntPtr.Zero || s_hOut == IntPtr.Zero)
            {
                TerminalUI.Write(s_hOut, "Failed to get handles\r\n");
                return;
            }

            if (!NativeConsole.GetConsoleMode(s_hIn, out s_originalMode))
            {
                TerminalUI.Write(s_hOut, "Failed to get console mode\r\n");
                return;
            }

            // Enable virtual terminal input and disable the processed modes
            s_mode = s_originalMode;
            s_mode |= NativeConsole.ENABLE_VIRTUAL_TERMINAL_INPUT | NativeConsole.ENABLE_MOUSE_INPUT;
            s_mode &= ~(NativeConsole.ENABLE_PROCESSED_INPUT | NativeConsole.ENABLE_LINE_INPUT |
                        NativeConsole.ENABLE_ECHO_INPUT | NativeConsole.ENABLE_QUICK_EDIT_MODE);

            if (!NativeConsole.SetConsoleMode(s_hIn, s_mode))
            {
                TerminalUI.Write(s_hOut, "Failed to set console mode\r\n");
                return;
            }

            // Enable VT processing on stdout so ANSI escape sequences are interpreted
            if (NativeConsole.GetConsoleMode(s_hOut, out uint outMode))
            {
                NativeConsole.SetConsoleMode(s_hOut, outMode | NativeConsole.ENABLE_VIRTUAL_TERMINAL_PROCESSING);
            }
        }
        else
        {
            // Unix: save stty state and set raw mode
            s_originalSttyState = TerminalUI.RunProcess("stty", "-g");
            TerminalUI.RunProcess("stty", "raw -echo -icanon -isig min 1");
        }

        (s_width, s_height) = TerminalUI.GetConsoleSize(s_hOut, s_isWindows);

        TerminalUI.EnableMouseTracking(s_hOut);
        TerminalUI.ClearScreen(s_hOut);

        // Draw header box and status lines
        s_statusRow = TerminalUI.DrawHeader(s_hOut, s_width, s_isWindows);
        TerminalUI.DrawStatusLine(s_hOut, s_statusRow, s_mode, s_isWindows);

        s_reader = s_isWindows
            ? new NativeInputReader(s_hIn)
            : new StreamInputReader(s_isWindows);

        s_methodRow = s_statusRow + 1;
        TerminalUI.DrawMethodLine(s_hOut, s_methodRow, s_reader.DisplayName);

        // Signal mode line (row after method line)
        int signalRow = s_methodRow + 1;
        s_signalMode = false;
        TerminalUI.DrawSignalLine(s_hOut, signalRow, s_signalMode);

        // Register SIGCONT handler for Unix suspend/resume
        PosixSignalRegistration? sigcontReg = null;

        PosixSignalRegistration? sigintReg = null;

        if (!s_isWindows)
        {
#pragma warning disable CA1416 // Platform compatibility — guarded by s_isWindows check
            sigcontReg = PosixSignalRegistration.Create(PosixSignal.SIGCONT, _ =>
            {
                // Shell restored cooked mode; re-apply raw mode
                if (s_signalMode)
                    TerminalUI.RunProcess("stty", "raw -echo -icanon isig min 1");
                else
                    TerminalUI.RunProcess("stty", "raw -echo -icanon -isig min 1");

                // Re-enable mouse and redraw UI
                TerminalUI.EnableMouseTracking(s_hOut);
                TerminalUI.ClearScreen(s_hOut);
                s_statusRow = TerminalUI.DrawHeader(s_hOut, s_width, s_isWindows);
                TerminalUI.DrawStatusLine(s_hOut, s_statusRow, s_mode, s_isWindows);
                TerminalUI.DrawMethodLine(s_hOut, s_methodRow, s_reader.DisplayName);
                TerminalUI.DrawSignalLine(s_hOut, signalRow, s_signalMode);
                TerminalUI.SetScrollRegion(s_hOut, s_scrollTop, s_height);
            });

            sigintReg = PosixSignalRegistration.Create(PosixSignal.SIGINT, ctx =>
            {
                ctx.Cancel = true; // Prevent default termination
                Cleanup();
                Environment.Exit(0);
            });
#pragma warning restore CA1416
        }

        // Set scroll region to rows below the header, leaving header fixed
        s_scrollTop = signalRow + 2;
        TerminalUI.SetScrollRegion(s_hOut, s_scrollTop, s_height);

        var buffer = new byte[256];

        while (true)
        {
            int bytesRead = s_reader.Read(buffer);

            if (bytesRead < 0)
                break; // native read failed

            if (bytesRead == 0)
            {
                TerminalUI.Write(s_hOut, s_reader.ZeroBytesMessage + "\r\n");
                continue;
            }

            var pretty = AnsiSequenceParser.FormatInput(buffer, bytesRead);
            var hex = AnsiSequenceParser.FormatRawHex(buffer, bytesRead);
            TerminalUI.Write(s_hOut, $"{s_reader.Label}: {pretty}  \x1b[90m({hex})\x1b[0m\r\n");

            var chunk = Encoding.UTF8.GetString(buffer, 0, bytesRead);

            if (chunk.Contains('q'))
                break;

            if (chunk.Contains('s'))
            {
                s_reader.Dispose();
                s_reader = NextReader(s_reader);
                TerminalUI.UpdateMethodLine(s_hOut, s_methodRow, s_reader.DisplayName);
            }

            if (chunk.Contains('z'))
            {
                s_signalMode = !s_signalMode;

                if (!s_isWindows)
                {
                    // Toggle isig: when on, kernel handles Ctrl+Z (SIGTSTP) and Ctrl+C (SIGINT)
                    if (s_signalMode)
                        TerminalUI.RunProcess("stty", "isig");
                    else
                        TerminalUI.RunProcess("stty", "-isig");
                }
                else
                {
                    // Toggle ENABLE_PROCESSED_INPUT: when on, Windows handles Ctrl+C
                    if (s_signalMode)
                        s_mode |= NativeConsole.ENABLE_PROCESSED_INPUT;
                    else
                        s_mode &= ~NativeConsole.ENABLE_PROCESSED_INPUT;

                    NativeConsole.SetConsoleMode(s_hIn, s_mode);
                }

                TerminalUI.UpdateSignalLine(s_hOut, signalRow, s_signalMode);
            }

            if (chunk.Contains('c'))
                TerminalUI.ClearScrollRegion(s_hOut, s_scrollTop);
        }

        sigintReg?.Dispose();
        sigcontReg?.Dispose();
        Cleanup();
    }

    /// <summary>
    /// Cycles to the next input reader.
    /// Windows: NativeInputReader → StreamInputReader → AsyncStreamInputReader → …
    /// Unix:    StreamInputReader → AsyncStreamInputReader → …
    /// </summary>
    private static IInputReader NextReader(IInputReader current) => current switch
    {
        NativeInputReader => new StreamInputReader(s_isWindows),
        StreamInputReader => new AsyncStreamInputReader(s_isWindows),
        AsyncStreamInputReader when s_isWindows => new NativeInputReader(s_hIn),
        _ => new StreamInputReader(s_isWindows),
    };

    private static void Cleanup()
    {
        s_reader.Dispose();

        TerminalUI.ResetScrollRegion(s_hOut);
        TerminalUI.DisableMouseTracking(s_hOut);

        if (s_isWindows)
        {
            NativeConsole.SetConsoleMode(s_hIn, s_originalMode);
        }
        else if (s_originalSttyState != null)
        {
            TerminalUI.RunProcess("stty", s_originalSttyState);
        }

        TerminalUI.Write(s_hOut, $"\x1b[{s_height};1H\r\nRestored console mode.\r\n");
        TerminalUI.DisposeStdoutStream();
    }
}
