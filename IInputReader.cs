namespace VTTest;

/// <summary>
/// Abstracts the two input-reading strategies (Stream vs. native ReadFile)
/// so each can live in its own file with independent logic.
/// </summary>
internal interface IInputReader : IDisposable
{
    /// <summary>Short label for log output (e.g. "Strm" or "RdFl").</summary>
    string Label { get; }

    /// <summary>Display name shown in the header (e.g. "Stream (Console.OpenStandardInput)").</summary>
    string DisplayName { get; }

    /// <summary>Message shown when Read returns 0 bytes.</summary>
    string ZeroBytesMessage { get; }

    /// <summary>
    /// Reads raw input into <paramref name="buffer"/>.
    /// Returns the number of bytes read, or -1 if the underlying read failed (caller should break).
    /// </summary>
    int Read(byte[] buffer);
}
