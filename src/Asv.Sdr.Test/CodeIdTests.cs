using System;
using System.Collections.Generic;
using System.Reactive.Subjects;
using System.Threading;
using Xunit;

namespace Asv.Sdr.Test
{
    public class CodeIdTests
    {
        [Fact]
        public void CodeId_AutoDotTime_DecodesWithoutDotTime()
        {
            using var src = new Subject<double>();
            using var done = new AutoResetEvent(false);
            var received = new List<CodeId>();

            src.CodeId(0.05, 0.8, 100, 10_000).Subscribe(x =>
            {
                received.Add(x);
                done.Set();
            });

            Push(src, 0.3, 10);
            Push(src, 0.0, 10);
            Push(src, 0.3, 30);
            Push(src, 0.0, 70);
            Push(src, 0.3, 1);

            Assert.True(done.WaitOne(TimeSpan.FromSeconds(1)));
            Assert.Single(received);
            Assert.Equal("A", received[0].Value);
            Assert.Equal(100, received[0].DotTimeMs, 5);
            Assert.Equal(300, received[0].DashTimeMs, 5);
        }

        private static void Push(ISubject<double> src, double value, int count)
        {
            for (var i = 0; i < count; i++)
            {
                src.OnNext(value);
            }
        }
    }
}
