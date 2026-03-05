using System.Runtime.InteropServices;
using System.Text;

namespace VTTest;

internal class Program
{
    // Console input mode flags (from wincon.h)
    private const uint ENABLE_PROCESSED_INPUT = 0x0001;
    private const uint ENABLE_LINE_INPUT = 0x0002;
    private const uint ENABLE_ECHO_INPUT = 0x0004;
    private const uint ENABLE_WINDOW_INPUT = 0x0008;
    private const uint ENABLE_MOUSE_INPUT = 0x0010;
    private const uint ENABLE_INSERT_MODE = 0x0020;
    private const uint ENABLE_QUICK_EDIT_MODE = 0x0040;
    private const uint ENABLE_EXTENDED_FLAGS = 0x0080;
    private const uint ENABLE_AUTO_POSITION = 0x0100;
    private const uint ENABLE_VIRTUAL_TERMINAL_INPUT = 0x0200;

    private const int STD_INPUT_HANDLE = -10;
    private const int STD_OUTPUT_HANDLE = -11;

    [DllImport ("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetStdHandle (int nStdHandle);

    [DllImport ("kernel32.dll", SetLastError = true)]
    private static extern bool GetConsoleMode (IntPtr hConsoleHandle, out uint lpMode);

    [DllImport ("kernel32.dll", SetLastError = true)]
    private static extern bool SetConsoleMode (IntPtr hConsoleHandle, uint dwMode);

    [DllImport ("kernel32.dll", SetLastError = true)]
    private static extern bool ReadFile (
                                         IntPtr hFile,
                                         byte [] lpBuffer,
                                         uint nNumberOfBytesToRead,
                                         out uint lpNumberOfBytesRead,
                                         IntPtr lpOverlapped
                                        );

    [DllImport ("kernel32.dll", SetLastError = true)]
    private static extern bool WriteFile (
                                          IntPtr hFile,
                                          byte [] lpBuffer,
                                          uint nNumberOfBytesToWrite,
                                          out uint lpNumberOfBytesWritten,
                                          IntPtr lpOverlapped
                                         );

    [StructLayout (LayoutKind.Sequential)]
    private struct CONSOLE_SCREEN_BUFFER_INFO
    {
        public COORD dwSize;
        public COORD dwCursorPosition;
        public short wAttributes;
        public SMALL_RECT srWindow;
        public COORD dwMaximumWindowSize;
    }

    [StructLayout (LayoutKind.Sequential)]
    private struct COORD
    {
        public short X;
        public short Y;
    }

    [StructLayout (LayoutKind.Sequential)]
    private struct SMALL_RECT
    {
        public short Left;
        public short Top;
        public short Right;
        public short Bottom;
    }

    [DllImport ("kernel32.dll", SetLastError = true)]
    private static extern bool GetConsoleScreenBufferInfo (
                                                           IntPtr hConsoleOutput,
                                                           out CONSOLE_SCREEN_BUFFER_INFO lpConsoleScreenBufferInfo
                                                          );

    private static void Write (IntPtr hOut, string text)
    {
        byte [] bytes = Encoding.UTF8.GetBytes (text);
        WriteFile (hOut, bytes, (uint)bytes.Length, out uint _, IntPtr.Zero);
    }

    // Known CSI key sequences: ESC [ <params> <final>
    // Modifier encoding: param = 1 + modifier bits
    //   Shift=1, Alt=2, Ctrl=4 → param 2=Shift, 3=Alt, 4=Shift+Alt, 5=Ctrl, 6=Ctrl+Shift, 7=Ctrl+Alt, 8=Ctrl+Shift+Alt
    private static readonly Dictionary<char, string> s_csiKeyNames = new ()
    {
        { 'A', "Up" }, { 'B', "Down" }, { 'C', "Right" }, { 'D', "Left" },
        { 'H', "Home" }, { 'F', "End" }, { 'P', "F1" }, { 'Q', "F2" },
        { 'R', "F3" }, { 'S', "F4" }, { 'Z', "Shift+Tab" }
    };

    // Tilde sequences: ESC [ <number> ~ or ESC [ <number> ; <modifier> ~
    private static readonly Dictionary<int, string> s_tildeKeyNames = new ()
    {
        { 1, "Home" }, { 2, "Insert" }, { 3, "Delete" }, { 4, "End" },
        { 5, "PageUp" }, { 6, "PageDown" },
        { 11, "F1" }, { 12, "F2" }, { 13, "F3" }, { 14, "F4" },
        { 15, "F5" }, { 17, "F6" }, { 18, "F7" }, { 19, "F8" },
        { 20, "F9" }, { 21, "F10" }, { 23, "F11" }, { 24, "F12" }
    };

    // Control character names (0x00-0x1F)
    private static readonly Dictionary<int, string> s_ctrlCharNames = new ()
    {
        { 0x00, "Ctrl+@" }, { 0x01, "Ctrl+A" }, { 0x02, "Ctrl+B" }, { 0x03, "Ctrl+C" },
        { 0x04, "Ctrl+D" }, { 0x05, "Ctrl+E" }, { 0x06, "Ctrl+F" }, { 0x07, "Ctrl+G (BEL)" },
        { 0x08, "Ctrl+H (BS)" }, { 0x09, "Tab" }, { 0x0A, "Ctrl+J (LF)" }, { 0x0B, "Ctrl+K" },
        { 0x0C, "Ctrl+L" }, { 0x0D, "Enter (CR)" }, { 0x0E, "Ctrl+N" }, { 0x0F, "Ctrl+O" },
        { 0x10, "Ctrl+P" }, { 0x11, "Ctrl+Q" }, { 0x12, "Ctrl+R" }, { 0x13, "Ctrl+S" },
        { 0x14, "Ctrl+T" }, { 0x15, "Ctrl+U" }, { 0x16, "Ctrl+V" }, { 0x17, "Ctrl+W" },
        { 0x18, "Ctrl+X" }, { 0x19, "Ctrl+Y" }, { 0x1A, "Ctrl+Z" }, { 0x1B, "Escape" },
        { 0x1C, "Ctrl+\\" }, { 0x1D, "Ctrl+]" }, { 0x1E, "Ctrl+^" }, { 0x1F, "Ctrl+_" }
    };

    private static string GetModifierPrefix (int modParam)
    {
        // modParam = 1 + modifier bits. Shift=1, Alt=2, Ctrl=4
        int bits = modParam - 1;
        var parts = new List<string> ();

        if ((bits & 4) != 0)
        {
            parts.Add ("Ctrl");
        }

        if ((bits & 2) != 0)
        {
            parts.Add ("Alt");
        }

        if ((bits & 1) != 0)
        {
            parts.Add ("Shift");
        }

        return parts.Count > 0 ? string.Join ("+", parts) + "+" : "";
    }

    private static string FormatInput (byte [] buffer, int count)
    {
        var text = Encoding.UTF8.GetString (buffer, 0, count);
        var result = new StringBuilder ();
        var i = 0;

        while (i < text.Length)
        {
            if (result.Length > 0)
            {
                result.Append ("  ");
            }

            // ESC sequence
            if (text [i] == '\x1b' && i + 1 < text.Length)
            {
                // CSI sequence: ESC [
                if (text [i + 1] == '[')
                {
                    int start = i + 2;
                    var paramStr = new StringBuilder ();

                    int j = start;

                    while (j < text.Length && (char.IsDigit (text [j]) || text [j] == ';'))
                    {
                        paramStr.Append (text [j]);
                        j++;
                    }

                    if (j < text.Length)
                    {
                        char final = text [j];

                        // SGR mouse: ESC [ < ...
                        if (final == '<')
                        {
                            // Consume the rest of the mouse sequence
                            j++; // skip '<'
                            var mouseParams = new StringBuilder ();

                            while (j < text.Length && text [j] != 'M' && text [j] != 'm')
                            {
                                mouseParams.Append (text [j]);
                                j++;
                            }

                            if (j < text.Length)
                            {
                                char mFinal = text [j];
                                var parts = mouseParams.ToString ().Split (';');

                                if (parts.Length >= 3 && int.TryParse (parts [0], out int btn))
                                {
                                    var action = mFinal == 'M' ? "press" : "release";
                                    var btnName = (btn & 0x3) switch
                                    {
                                        0 => "Left",
                                        1 => "Middle",
                                        2 => "Right",
                                        _ => $"Btn{btn & 0x3}"
                                    };

                                    if ((btn & 64) != 0)
                                    {
                                        btnName = (btn & 0x1) == 0 ? "WheelUp" : "WheelDown";
                                    }

                                    if ((btn & 32) != 0)
                                    {
                                        action = "move";
                                    }

                                    var mods = new List<string> ();

                                    if ((btn & 4) != 0)
                                    {
                                        mods.Add ("Shift");
                                    }

                                    if ((btn & 8) != 0)
                                    {
                                        mods.Add ("Alt");
                                    }

                                    if ((btn & 16) != 0)
                                    {
                                        mods.Add ("Ctrl");
                                    }

                                    var modStr = mods.Count > 0 ? string.Join ("+", mods) + "+" : "";
                                    result.Append ($"[Mouse {modStr}{btnName} {action} @{parts [1]},{parts [2]}]");
                                }
                                else
                                {
                                    result.Append ($"[Mouse ESC[<{mouseParams}{mFinal}]");
                                }

                                i = j + 1;

                                continue;
                            }
                        }

                        // Tilde sequence: ESC [ <num> ~ or ESC [ <num> ; <mod> ~
                        if (final == '~')
                        {
                            var parts = paramStr.ToString ().Split (';');

                            if (int.TryParse (parts [0], out int keyNum) && s_tildeKeyNames.TryGetValue (keyNum, out var keyName))
                            {
                                var mod = parts.Length > 1 && int.TryParse (parts [1], out int m) ? GetModifierPrefix (m) : "";
                                result.Append ($"[{mod}{keyName}]");
                            }
                            else
                            {
                                result.Append ($"[ESC[{paramStr}~]");
                            }

                            i = j + 1;

                            continue;
                        }

                        // Letter-final CSI: ESC [ <params> <letter>
                        if (char.IsLetter (final))
                        {
                            var parts = paramStr.ToString ().Split (';');

                            if (s_csiKeyNames.TryGetValue (final, out var keyName))
                            {
                                var mod = parts.Length > 1 && int.TryParse (parts [1], out int m) ? GetModifierPrefix (m) : "";
                                result.Append ($"[{mod}{keyName}]");
                            }
                            else
                            {
                                // F1-F4 with modifiers: ESC [ 1 ; <mod> P/Q/R/S
                                result.Append ($"[CSI {paramStr}{final}]");
                            }

                            i = j + 1;

                            continue;
                        }
                    }

                    // Fallback: couldn't parse CSI fully
                    result.Append ($"[ESC[{paramStr}");

                    if (j < text.Length)
                    {
                        result.Append (text [j]);
                        i = j + 1;
                    }
                    else
                    {
                        i = j;
                    }

                    result.Append ("]");

                    continue;
                }

                // SS3 sequence: ESC O <letter> (F1-F4 on some terminals)
                if (text [i + 1] == 'O' && i + 2 < text.Length)
                {
                    char key = text [i + 2];
                    var name = key switch
                    {
                        'P' => "F1",
                        'Q' => "F2",
                        'R' => "F3",
                        'S' => "F4",
                        _ => $"SS3-{key}"
                    };
                    result.Append ($"[{name}]");
                    i += 3;

                    continue;
                }

                // Alt+key: ESC <char>
                if (text [i + 1] >= 0x20)
                {
                    result.Append ($"[Alt+{text [i + 1]}]");
                    i += 2;

                    continue;
                }

                // Bare ESC followed by control char
                result.Append ("[Escape]");
                i++;

                continue;
            }

            // Control characters
            if (text [i] < 0x20)
            {
                if (s_ctrlCharNames.TryGetValue (text [i], out var name))
                {
                    result.Append ($"[{name}]");
                }
                else
                {
                    result.Append ($"[0x{(int)text [i]:X2}]");
                }

                i++;

                continue;
            }

            // DEL
            if (text [i] == 0x7F)
            {
                result.Append ("[Backspace]");
                i++;

                continue;
            }

            // Printable character
            result.Append (text [i]);
            i++;
        }

        return result.ToString ();
    }

    private static string FormatRawHex (byte [] buffer, int count)
    {
        var sb = new StringBuilder ();

        for (var i = 0; i < count; i++)
        {
            if (i > 0)
            {
                sb.Append (' ');
            }

            sb.Append ($"{buffer [i]:X2}");
        }

        return sb.ToString ();
    }

    private static void Main ()
    {
        IntPtr hIn = GetStdHandle (STD_INPUT_HANDLE);
        IntPtr hOut = GetStdHandle (STD_OUTPUT_HANDLE);

        if (hIn == IntPtr.Zero || hOut == IntPtr.Zero)
        {
            Write (hOut, "Failed to get handles\r\n");

            return;
        }

        if (!GetConsoleMode (hIn, out uint originalMode))
        {
            Write (hOut, "Failed to get console mode\r\n");

            return;
        }

        // Enable virtual terminal input and disable the processed modes
        uint mode = originalMode;
        mode |= ENABLE_VIRTUAL_TERMINAL_INPUT | ENABLE_MOUSE_INPUT;
        mode &= ~(ENABLE_PROCESSED_INPUT | ENABLE_LINE_INPUT | ENABLE_ECHO_INPUT | ENABLE_QUICK_EDIT_MODE);

        if (!SetConsoleMode (hIn, mode))
        {
            Write (hOut, "Failed to set console mode\r\n");

            return;
        }

        // Get console size
        GetConsoleScreenBufferInfo (hOut, out CONSOLE_SCREEN_BUFFER_INFO csbi);
        var width = csbi.srWindow.Right - csbi.srWindow.Left + 1;
        var height = csbi.srWindow.Bottom - csbi.srWindow.Top + 1;

        // Enable mouse tracking (any event + SGR encoding)
        Write (hOut, "\x1b[?1003h\x1b[?1006h");

        // Clear screen
        Write (hOut, "\x1b[2J\x1b[H");

        // Draw header box (rows 1-7)
        var boxWidth = Math.Min (80, width - 2);
        var leftMargin = (width - boxWidth) / 2;

        Write (hOut, $"\x1b[1;{leftMargin}H");
        Write (hOut, "┌" + new string ('─', boxWidth - 2) + "┐");

        string [] instructions =
        {
            "VT Input Test - Raw ANSI Escape Sequences",
            "",
            "Try: Arrow keys, Function keys, Mouse, Ctrl+Z",
            "'s' = toggle Stream/ReadFile, 'c' = clear, 'q' = quit"
        };

        for (var i = 0; i < instructions.Length; i++)
        {
            Write (hOut, $"\x1b[{2 + i};{leftMargin}H");
            var line = instructions [i];
            var padding = (boxWidth - 2 - line.Length) / 2;

            Write (
                   hOut,
                   "│" + new string (' ', padding) + line +
                   new string (' ', boxWidth - 2 - padding - line.Length) + "│"
                  );
        }

        Write (hOut, $"\x1b[{2 + instructions.Length};{leftMargin}H");
        Write (hOut, "└" + new string ('─', boxWidth - 2) + "┘");

        // Status line (row after box)
        int statusRow = 2 + instructions.Length + 1;

        Write (hOut, $"\x1b[{statusRow};1H");
        Write (hOut, $"Mode: 0x{mode:X} | VT:{((mode & ENABLE_VIRTUAL_TERMINAL_INPUT) != 0 ? "ON" : "OFF")} Mouse:{((mode & ENABLE_MOUSE_INPUT) != 0 ? "ON" : "OFF")}");

        var useStream = false;
        Stream? stdinStream = null;

        int methodRow = statusRow + 1;
        Write (hOut, $"\x1b[{methodRow};1H");
        Write (hOut, "Read: ReadFile (P/Invoke)");

        // Set scroll region to rows below the header, leaving header fixed
        int scrollTop = methodRow + 2;
        Write (hOut, $"\x1b[{scrollTop};{height}r"); // Set scroll region
        Write (hOut, $"\x1b[{scrollTop};1H"); // Position cursor in scroll region

        var buffer = new byte [256];

        while (true)
        {
            int bytesRead;

            if (useStream)
            {
                stdinStream ??= Console.OpenStandardInput ();
                bytesRead = stdinStream.Read (buffer, 0, buffer.Length);

                if (bytesRead == 0)
                {
                    Write (hOut, "[Ctrl+Z] (Stream returned 0 bytes - Windows EOF bug)\r\n");

                    continue;
                }
            }
            else
            {
                if (!ReadFile (hIn, buffer, (uint)buffer.Length, out uint nativeRead, IntPtr.Zero))
                {
                    break;
                }

                bytesRead = (int)nativeRead;

                if (bytesRead == 0)
                {
                    Write (hOut, "[Ctrl+Z] (ReadFile returned 0 bytes - Windows EOF bug)\r\n");

                    continue;
                }
            }

            var label = useStream ? "Strm" : "RdFl";
            var pretty = FormatInput (buffer, bytesRead);
            var hex = FormatRawHex (buffer, bytesRead);
            Write (hOut, $"{label}: {pretty}  \x1b[90m({hex})\x1b[0m\r\n");

            var chunk = Encoding.UTF8.GetString (buffer, 0, bytesRead);

            if (chunk.Contains ('q'))
            {
                break;
            }

            if (chunk.Contains ('s'))
            {
                useStream = !useStream;

                if (!useStream)
                {
                    stdinStream?.Dispose ();
                    stdinStream = null;
                }

                // Update method display in the fixed header area
                // Save cursor, move to method row, clear line, write, restore cursor
                var methodName = useStream ? "Stream (Console.OpenStandardInput)" : "ReadFile (P/Invoke)";
                Write (hOut, $"\x1b[s\x1b[{methodRow};1H\x1b[2KRead: {methodName}\x1b[u");
            }

            if (chunk.Contains ('c'))
            {
                // Clear scroll region
                Write (hOut, $"\x1b[{scrollTop};1H\x1b[J");
            }
        }

        stdinStream?.Dispose ();

        // Reset scroll region to full screen
        Write (hOut, "\x1b[r");

        // Restore original mode
        SetConsoleMode (hIn, originalMode);
        Write (hOut, "\x1b[?1003l\x1b[?1006l"); // disable mouse tracking
        Write (hOut, $"\x1b[{height};1H\r\nRestored console mode.\r\n");
    }
}
