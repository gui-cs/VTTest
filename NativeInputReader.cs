namespace VTTest;

/// <summary>
/// Reads console input via the Win32 <c>ReadFile</c> P/Invoke,
/// bypassing .NET's stream layer entirely.
/// </summary>
internal sealed class NativeInputReader : IInputReader
{
    private readonly IntPtr _hIn;

    internal NativeInputReader(IntPtr hIn)
    {
        _hIn = hIn;
    }

    public string Label => "RdFl";
    public string DisplayName => "ReadFile (P/Invoke)";
    public string ZeroBytesMessage => "[Ctrl+Z] (ReadFile returned 0 bytes - Windows EOF bug)";

    public int Read(byte[] buffer)
    {
        if (!NativeConsole.ReadFile(_hIn, buffer, (uint)buffer.Length, out uint nativeRead, IntPtr.Zero))
            return -1; // read failed — caller should break

        return (int)nativeRead;
    }

    public void Dispose()
    {
        // Nothing to dispose — the console handle is owned by the OS.
    }
}
