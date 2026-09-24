using System;
using DotNetCommander;
using Xunit;

namespace DotNetCommander.Tests
{
    public sealed class DirectoryRefreshCoalescerTests
    {
        private const int Quiet = 350;
        private const int MaxLatency = 1500;

        [Fact]
        public void NoEvents_TryFire_ReturnsFalse()
        {
            var coalescer = Create();

            Assert.False(coalescer.TryFire(100000));
        }

        [Fact]
        public void SingleEvent_FiresOnlyAfterQuietWindow()
        {
            var coalescer = Create();
            coalescer.NotifyEvent(1000);

            Assert.False(coalescer.TryFire(1000 + Quiet - 1));
            Assert.True(coalescer.TryFire(1000 + Quiet));
        }

        [Fact]
        public void AfterFiring_NoPendingUntilNextEvent()
        {
            var coalescer = Create();
            coalescer.NotifyEvent(1000);

            Assert.True(coalescer.TryFire(1000 + Quiet));
            Assert.False(coalescer.TryFire(100000));
        }

        [Fact]
        public void TooEarlyFire_KeepsPendingForLaterPoll()
        {
            var coalescer = Create();
            coalescer.NotifyEvent(1000);

            Assert.False(coalescer.TryFire(1100));
            Assert.True(coalescer.TryFire(1000 + Quiet));
        }

        [Fact]
        public void BurstEvents_CoalesceIntoSingleRefresh()
        {
            var coalescer = Create();
            coalescer.NotifyEvent(1000);
            coalescer.NotifyEvent(1100);
            coalescer.NotifyEvent(1200);

            Assert.False(coalescer.TryFire(1200 + Quiet - 1));
            Assert.True(coalescer.TryFire(1200 + Quiet));
            Assert.False(coalescer.TryFire(1200 + Quiet + 1));
        }

        [Fact]
        public void ContinuousStorm_FiresAtMaxLatencyCap()
        {
            var coalescer = Create();
            long start = 0;
            for (long now = start; now < start + MaxLatency; now += 50)
            {
                coalescer.NotifyEvent(now);
                Assert.False(coalescer.TryFire(now));
            }

            Assert.False(coalescer.TryFire(start + MaxLatency - 1));
            Assert.True(coalescer.TryFire(start + MaxLatency));
        }

        [Fact]
        public void Reset_ClearsPendingEvents()
        {
            var coalescer = Create();
            coalescer.NotifyEvent(1000);

            coalescer.Reset();

            Assert.False(coalescer.TryFire(100000));
        }

        [Fact]
        public void NewCycleAfterFire_Works()
        {
            var coalescer = Create();
            coalescer.NotifyEvent(1000);
            Assert.True(coalescer.TryFire(1000 + Quiet));

            coalescer.NotifyEvent(5000);
            Assert.False(coalescer.TryFire(5000 + Quiet - 1));
            Assert.True(coalescer.TryFire(5000 + Quiet));
        }

        [Fact]
        public void InvalidConfiguration_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new DirectoryRefreshCoalescer(0, MaxLatency));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DirectoryRefreshCoalescer(Quiet, Quiet - 1));
        }

        private static DirectoryRefreshCoalescer Create()
        {
            return new DirectoryRefreshCoalescer(Quiet, MaxLatency);
        }
    }
}
