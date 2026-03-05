using System.Runtime.InteropServices;

namespace VTTest;

/// <summary>
/// Win32 Console P/Invoke declarations.
/// </summary>
internal static class NativeConsole
{
    // Console input mode flags (from wincon.h)
    internal const uint ENABLE_PROCESSED_INPUT = 0x0001;
    internal const uint ENABLE_LINE_INPUT = 0x0002;
    internal const uint ENABLE_ECHO_INPUT = 0x0004;
    internal const uint ENABLE_WINDOW_INPUT = 0x0008;
    internal const uint ENABLE_MOUSE_INPUT = 0x0010;
    internal const uint ENABLE_INSERT_MODE = 0x0020;
    internal const uint ENABLE_QUICK_EDIT_MODE = 0x0040;
    internal const uint ENABLE_EXTENDED_FLAGS = 0x0080;
    internal const uint ENABLE_AUTO_POSITION = 0x0100;
    internal const uint ENABLE_VIRTUAL_TERMINAL_INPUT = 0x0200;

    internal const int STD_INPUT_HANDLE = -10;
    internal const int STD_OUTPUT_HANDLE = -11;

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern IntPtr GetStdHandle(int nStdHandle);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool ReadFile(
        IntPtr hFile,
        byte[] lpBuffer,
        uint nNumberOfBytesToRead,
        out uint lpNumberOfBytesRead,
        IntPtr lpOverlapped);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool WriteFile(
        IntPtr hFile,
        byte[] lpBuffer,
        uint nNumberOfBytesToWrite,
        out uint lpNumberOfBytesWritten,
        IntPtr lpOverlapped);

    [StructLayout(LayoutKind.Sequential)]
    internal struct CONSOLE_SCREEN_BUFFER_INFO
    {
        public COORD dwSize;
        public COORD dwCursorPosition;
        public short wAttributes;
        public SMALL_RECT srWindow;
        public COORD dwMaximumWindowSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct COORD
    {
        public short X;
        public short Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct SMALL_RECT
    {
        public short Left;
        public short Top;
        public short Right;
        public short Bottom;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool GetConsoleScreenBufferInfo(
        IntPtr hConsoleOutput,
        out CONSOLE_SCREEN_BUFFER_INFO lpConsoleScreenBufferInfo);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool SetConsoleOutputCP(uint wCodePageID);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern uint GetConsoleOutputCP();

    internal const uint CP_UTF8 = 65001;
}
