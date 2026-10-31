using System.IO.Pipes;
using System.Text;

namespace Gili.Platform.Claude;

/// <summary>
/// \\.\pipe\Gili.Claude, where <c>Gili.ClaudeHook</c> writes one JSON line per hook (PRD 9.2). Only the same user can
/// connect. Lines arrive on a thread-pool thread.
/// </summary>
public sealed class ClaudePipeServer : IDisposable
{
    public const string PipeName = "Gili.Claude";

    /// <summary>A hook line is a few hundred bytes; anything far longer is not from the helper.</summary>
    private const int MaxLine = 64 * 1024;

    private readonly CancellationTokenSource _stop = new();

    public event Action<string>? LineReceived;

    /// <summary>Raised once when the pipe cannot be created, e.g. another copy of Gili already owns it.</summary>
    public event Action<Exception>? Failed;

    public void Start() => _ = AcceptAsync(_stop.Token);

    public void Dispose()
    {
        _stop.Cancel();
        _stop.Dispose();
    }

    private async Task AcceptAsync(CancellationToken stop)
    {
        while (!stop.IsCancellationRequested)
        {
            NamedPipeServerStream server;
            try
            {
                server = new NamedPipeServerStream(PipeName, PipeDirection.In, NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            }
            catch (IOException ex)
            {
                Failed?.Invoke(ex);
                return;
            }
            try
            {
                await server.WaitForConnectionAsync(stop).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                await server.DisposeAsync().ConfigureAwait(false);
                return;
            }
            catch (IOException)
            {
                await server.DisposeAsync().ConfigureAwait(false);
                continue;
            }
            _ = ReadAsync(server, stop);
        }
    }

    private async Task ReadAsync(NamedPipeServerStream server, CancellationToken stop)
    {
        await using (server.ConfigureAwait(false))
        {
            try
            {
                using var reader = new StreamReader(server, Encoding.UTF8);
                var buffer = new char[MaxLine];
                var read = await reader.ReadBlockAsync(buffer.AsMemory(), stop).ConfigureAwait(false);
                var text = new string(buffer, 0, read);
                foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    LineReceived?.Invoke(line);
            }
            catch (Exception ex) when (ex is IOException or OperationCanceledException or ObjectDisposedException)
            {
                // The helper went away mid-line: nothing to show.
            }
        }
    }
}
