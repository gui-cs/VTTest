using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace VTTest;

/// <summary>
/// Reads console input asynchronously via <see cref="Stream.ReadAsync"/> on a
/// background task, buffering results into a <see cref="BlockingCollection{T}"/>.
/// The synchronous <see cref="Read"/> method blocks until buffered data is available.
///
/// This demonstrates the pattern proposed for Terminal.Gui's WindowsVTInputHelper:
/// a background async reader feeds a concurrent buffer, while consumers use a
/// synchronous Peek/Read API backed by the buffer.
/// </summary>
internal sealed class AsyncStreamInputReader : IInputReader
{
    private readonly bool _isWindows;
    private readonly CancellationTokenSource _cts = new();
    private readonly BlockingCollection<(byte[] Data, int Count)> _queue = new();
    private Stream? _stdinStream;
    private Task? _readTask;

    internal AsyncStreamInputReader(bool isWindows)
    {
        _isWindows = isWindows;
        Start();
    }

    public string Label => "Async";
    public string DisplayName => "Async Stream (ReadAsync + ConcurrentQueue)";
    public string ZeroBytesMessage => "[Ctrl+Z] (Async Stream returned 0 bytes - Windows EOF bug)";

    public int Read(byte[] buffer)
    {
        try
        {
            // Block until the background task has enqueued data (or cancellation).
            var (data, count) = _queue.Take(_cts.Token);
            int toCopy = Math.Min(count, buffer.Length);
            Array.Copy(data, 0, buffer, 0, toCopy);
            return toCopy;
        }
        catch (OperationCanceledException)
        {
            return -1;
        }
        catch (InvalidOperationException)
        {
            // Collection was marked complete — background task exited.
            return -1;
        }
    }

    private void Start()
    {
        _stdinStream = _isWindows
            ? Console.OpenStandardInput()
            : new FileStream(
                new SafeFileHandle((IntPtr)0, ownsHandle: false),
                FileAccess.Read,
                bufferSize: 1);

        _readTask = Task.Run(async () =>
        {
            var buf = new byte[256];
            try
            {
                while (!_cts.Token.IsCancellationRequested)
                {
                    int n = await _stdinStream.ReadAsync(buf.AsMemory(0, buf.Length), _cts.Token);

                    // Copy so the buffer can be reused on the next iteration.
                    var copy = new byte[n];
                    if (n > 0)
                        Array.Copy(buf, 0, copy, 0, n);

                    _queue.Add((copy, n), _cts.Token);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown.
            }
            catch (Exception)
            {
                // Stream error — signal the consumer.
            }
            finally
            {
                _queue.CompleteAdding();
            }
        });
    }

    public void Dispose()
    {
        _cts.Cancel();

        try
        {
            _readTask?.Wait(TimeSpan.FromSeconds(1));
        }
        catch (AggregateException)
        {
            // Swallow — task was cancelled.
        }

        _stdinStream?.Dispose();
        _stdinStream = null;
        _queue.Dispose();
        _cts.Dispose();
    }
}
