using System;

namespace DotNetCommander
{
    /// <summary>
    /// Thread-safe coalescing policy for directory watcher events.
    /// Watcher callbacks (any thread) only record timestamps; the UI timer
    /// polls <see cref="TryFire"/>. A refresh fires once the directory has
    /// been quiet for <c>quietMilliseconds</c>, or after
    /// <c>maxLatencyMilliseconds</c> even while events keep arriving, so
    /// rename series and copy storms can neither flood the UI nor starve
    /// the panel indefinitely.
    /// </summary>
    internal sealed class DirectoryRefreshCoalescer
    {
        private readonly object gate = new object();
        private readonly int quietMilliseconds;
        private readonly int maxLatencyMilliseconds;
        private bool pending;
        private long firstEventAt;
        private long lastEventAt;

        public DirectoryRefreshCoalescer(int quietMilliseconds, int maxLatencyMilliseconds)
        {
            if (quietMilliseconds <= 0)
                throw new ArgumentOutOfRangeException(nameof(quietMilliseconds));
            if (maxLatencyMilliseconds < quietMilliseconds)
                throw new ArgumentOutOfRangeException(nameof(maxLatencyMilliseconds));

            this.quietMilliseconds = quietMilliseconds;
            this.maxLatencyMilliseconds = maxLatencyMilliseconds;
        }

        public void NotifyEvent(long nowMilliseconds)
        {
            lock (gate)
            {
                if (!pending)
                {
                    pending = true;
                    firstEventAt = nowMilliseconds;
                }

                lastEventAt = nowMilliseconds;
            }
        }

        public bool TryFire(long nowMilliseconds)
        {
            lock (gate)
            {
                if (!pending)
                    return false;

                bool quietWindowElapsed = nowMilliseconds - lastEventAt >= quietMilliseconds;
                bool maxLatencyElapsed = nowMilliseconds - firstEventAt >= maxLatencyMilliseconds;
                if (!quietWindowElapsed && !maxLatencyElapsed)
                    return false;

                pending = false;
                return true;
            }
        }

        public void Reset()
        {
            lock (gate)
            {
                pending = false;
            }
        }
    }
}
