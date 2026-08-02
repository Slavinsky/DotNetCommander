using System;
using System.Threading;
using System.Threading.Tasks;

namespace DotNetCommander
{
    /// <summary>
    /// Process-wide sequential operation queue. The UI awaits the returned task without
    /// blocking, while destructive file-system plans cannot race each other.
    /// </summary>
    internal sealed class FileOperationQueue
    {
        private readonly SemaphoreSlim gate = new SemaphoreSlim(1, 1);

        public static FileOperationQueue Shared { get; } = new FileOperationQueue();

        public async Task<T> EnqueueAsync<T>(Func<T> operation, CancellationToken cancellationToken)
        {
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                return await Task.Run(operation).ConfigureAwait(false);
            }
            finally
            {
                gate.Release();
            }
        }
    }
}
