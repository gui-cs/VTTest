using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace VTTest;

/// <summary>
/// Reads console input via a .NET <see cref="Stream"/> —
/// <see cref="Console.OpenStandardInput()"/> on Windows, or a raw fd-0
/// <see cref="FileStream"/> on Unix to bypass .NET's buffered console stream
/// (which requires a newline before returning data).
/// </summary>
internal sealed class StreamInputReader : IInputReader
{
    private readonly bool _isWindows;
    private Stream? _stdinStream;

    internal StreamInputReader(bool isWindows)
    {
        _isWindows = isWindows;
    }

    public string Label => "Strm";
    public string DisplayName => "Stream (Console.OpenStandardInput)";
    public string ZeroBytesMessage => "[Ctrl+Z] (Stream returned 0 bytes - Windows EOF bug)";

    public int Read(byte[] buffer)
    {
        // Lazily open the stream so disposal + re-creation is cheap when toggling modes.
        _stdinStream ??= _isWindows
            ? Console.OpenStandardInput()
            : new FileStream(
                new SafeFileHandle((IntPtr)0, ownsHandle: false),
                FileAccess.Read,
                bufferSize: 1);

        return _stdinStream.Read(buffer, 0, buffer.Length);
    }

    public void Dispose()
    {
        _stdinStream?.Dispose();
        _stdinStream = null;
    }
}
