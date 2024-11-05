using System;
using System.Diagnostics;
using System.Threading;
using Asv.Sdr.Simulate;
using Xunit;
using Xunit.Abstractions;

namespace Asv.Sdr.Test
{
    public class UnitTest1
    {
        private readonly ITestOutputHelper _output;

        public UnitTest1(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void ParallelTest()
        {
            var virtualDevice = new VirtualReaderIqIls1F(86_000, 0.40, 0.90, DdmSdmType.AM90_150);

            var sampler = virtualDevice.Sample(86_000, out var start).Parallel();
                
            var src1 = sampler.Select((input, output) =>
            {
                input.CopyTo(output);
                _output.WriteLine($"FIRST:{DateTime.Now:O}");
                Thread.Sleep(5_000);
            });

            var src2 = sampler.Select((input, output) =>
            {
                input.CopyTo(output);
                _output.WriteLine($"SECOND:{DateTime.Now:O}");
                Thread.Sleep(7_000);
            });
            start();
            var sw = new Stopwatch();
            sw.Start();
            var eve = new AutoResetEvent(false);
            var source = src1.IqZip<double,double,double>(src2, (input1, input2, output) =>
            {
                _output.WriteLine($"ZIP:{DateTime.Now:O}");
                sw.Stop();
                eve.Set();
            }, 100);
            source.Subscribe(_ => { });
            eve.WaitOne();
            _output.WriteLine($"END:{DateTime.Now:O}");
            Assert.True(sw.Elapsed.TotalSeconds < 9);
            



        }
    }
}
