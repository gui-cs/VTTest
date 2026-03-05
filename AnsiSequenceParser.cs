using System.Text;

namespace VTTest;

/// <summary>
///     Parses raw byte input into human-readable ANSI sequence descriptions.
/// </summary>
internal static class AnsiSequenceParser
{
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

    internal static string GetModifierPrefix (int modParam)
    {
        // modParam = 1 + modifier bits. Shift=1, Alt=2, Ctrl=4
        int bits = modParam - 1;
        List<string> parts = [];

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

    internal static string FormatInput (byte [] buffer, int count)
    {
        var text = Encoding.UTF8.GetString (buffer, 0, count);
        StringBuilder result = new ();
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
                    StringBuilder paramStr = new ();

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
                            StringBuilder mouseParams = new ();

                            while (j < text.Length && text [j] != 'M' && text [j] != 'm')
                            {
                                mouseParams.Append (text [j]);
                                j++;
                            }

                            if (j < text.Length)
                            {
                                char mFinal = text [j];
                                string [] parts = mouseParams.ToString ().Split (';');

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

                                    List<string> mods = [];

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
                            string [] parts = paramStr.ToString ().Split (';');

                            if (int.TryParse (parts [0], out int keyNum) && s_tildeKeyNames.TryGetValue (keyNum, out string? keyName))
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
                            string [] parts = paramStr.ToString ().Split (';');

                            if (s_csiKeyNames.TryGetValue (final, out string? keyName))
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
                if (s_ctrlCharNames.TryGetValue (text [i], out string? name))
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

    internal static string FormatRawHex (byte [] buffer, int count)
    {
        StringBuilder sb = new ();

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
}
