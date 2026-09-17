using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;

namespace YappyNotes.App;

/// <summary>
/// Keeps YappyNotes to one running copy per notes folder, and lets a second start
/// wake the copy that is already running instead.
/// </summary>
/// <remarks>
/// <para>
/// A second copy is a way to lose typing, not just a spare window. Each copy loads
/// its own <c>Note</c> for every note on the desktop and autosaves it, so moving a
/// note in one writes that copy's older text over what was typed in the other.
/// "One note, one view-model" only holds within a process; this is what extends it
/// to the machine.
/// </para>
/// <para>
/// The claim is a lock on a file in the notes folder, not a named mutex, so that
/// it is per folder: a <c>dev-start.sh --sandbox</c> copy runs beside the real
/// one. The operating system drops the lock when the process ends however it
/// ends, so a crash cannot leave the folder claimed. The file is never deleted -
/// unlinking it while a third start has it open is how two copies would each end
/// up holding a lock on a different file of the same name. It is not in a temp
/// folder either, where a cleaner deleting it under an app that runs for weeks
/// would do the same.
/// </para>
/// <para>
/// Waking is a named pipe, and a connection is the whole message: nothing is read
/// from it, so nothing that connects can ask for anything but a window to come
/// forward. The pipe is restricted to the current user.
/// </para>
/// </remarks>
public sealed class SingleInstance : IDisposable
{
    private const string LockFileName = "yappynotes.lock";

    private readonly FileStream _lock;
    private readonly string _pipeName;
    private readonly CancellationTokenSource _stop = new();

    // Guards the pipe that is currently open, so that closing this copy and the
    // loop swapping one pipe for the next cannot each miss the other's.
    private readonly Lock _gate = new();
    private NamedPipeServerStream? _server;
    private bool _listening;
    private bool _disposed;

    private SingleInstance(FileStream lockFile, string pipeName)
    {
        _lock = lockFile;
        _pipeName = pipeName;
    }

    /// <summary>
    /// Claims the notes folder for this process.
    /// </summary>
    /// <returns>The claim, or null when another copy already holds the folder.</returns>
    public static SingleInstance? TryClaim(string dataDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);

        try
        {
            var lockFile = new FileStream(
                Path.Combine(dataDirectory, LockFileName),
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None);

            return new SingleInstance(lockFile, PipeNameFor(dataDirectory));
        }
        catch (IOException)
        {
            return null;
        }
    }

    /// <summary>
    /// Asks the copy holding this notes folder to come forward.
    /// </summary>
    /// <param name="patience">
    /// How long to keep trying. The running copy only listens once it has opened
    /// its database, so a start moments after another one has to wait for it.
    /// </param>
    /// <returns>Whether a running copy was reached.</returns>
    public static bool TryWakeRunningCopy(string dataDirectory, TimeSpan patience)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);

        using var client = new NamedPipeClientStream(
            ".", PipeNameFor(dataDirectory), PipeDirection.Out, PipeOptions.CurrentUserOnly);

        try
        {
            client.Connect(patience);
            return true;
        }
        catch (Exception e) when (e is TimeoutException or IOException)
        {
            return false;
        }
    }

    /// <summary>
    /// Calls <paramref name="onWake"/> each time another start asks this copy to
    /// come forward. It is called on a thread-pool thread.
    /// </summary>
    public void Listen(Action onWake)
    {
        ArgumentNullException.ThrowIfNull(onWake);

        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (_listening)
            {
                throw new InvalidOperationException("This copy is already listening.");
            }

            // The first pipe is opened here rather than on the thread pool, so this
            // copy can be reached the moment Listen returns. Opened inside the
            // task, it waited on a busy pool, and a start in that window found no
            // pipe for its whole patience and gave up.
            _listening = true;
            _server = NewServer();
        }

        _ = Task.Run(() => ListenAsync(onWake, _stop.Token));
    }

    private async Task ListenAsync(Action onWake, CancellationToken stop)
    {
        while (true)
        {
            NamedPipeServerStream? server;
            lock (_gate)
            {
                server = _server;
            }

            if (server is null)
            {
                return;
            }

            try
            {
                await server.WaitForConnectionAsync(stop);
            }
            catch (Exception e) when (e is OperationCanceledException or ObjectDisposedException or IOException)
            {
                // Closed under it by Dispose, which has already let go of the pipe.
                return;
            }

            lock (_gate)
            {
                if (_disposed)
                {
                    return;
                }

                // The next one opens before this one closes. On Unix a pipe is a
                // socket that exists only while a server holds it, so a start that
                // connected in the gap between the two was accepted by the old
                // socket and then silently dropped - the second of two quick starts
                // did nothing.
                _server = NewServer();
            }

            await server.DisposeAsync();
            onWake();
        }
    }

    private NamedPipeServerStream NewServer() => new(
        _pipeName,
        PipeDirection.In,
        NamedPipeServerStream.MaxAllowedServerInstances,
        PipeTransmissionMode.Byte,
        PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);

    /// <summary>
    /// Stops listening and lets go of the folder, in that order, so nothing is
    /// left answering on behalf of a copy that has gone.
    /// </summary>
    /// <remarks>
    /// The pipe is closed here, synchronously, rather than by asking the listening
    /// loop to stop and waiting for it. The loop needs a thread-pool thread to
    /// hear that, and under a busy pool the wait ran out with the pipe still open:
    /// the folder was free and something still answered for it.
    /// </remarks>
    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _server?.Dispose();
            _server = null;
        }

        _stop.Cancel();
        _stop.Dispose();
        _lock.Dispose();
    }

    /// <summary>
    /// A pipe name for this notes folder. Hashed, because a Unix pipe is a socket
    /// file whose whole path must fit in about a hundred characters.
    /// </summary>
    private static string PipeNameFor(string dataDirectory)
    {
        var folder = Path.TrimEndingDirectorySeparator(Path.GetFullPath(dataDirectory));

        // The same folder spelled in another case is the same folder on the file
        // systems Windows and macOS use by default.
        if (OperatingSystem.IsWindows() || OperatingSystem.IsMacOS())
        {
            folder = folder.ToUpperInvariant();
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(folder));
        return $"yappynotes-{Convert.ToHexStringLower(hash)[..16]}";
    }
}
