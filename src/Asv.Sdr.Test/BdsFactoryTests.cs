using System;
using System.Collections.Generic;
using Xunit;

namespace Asv.Sdr.Test
{
    public class BdsFactoryTests
    {
        [Fact]
        public void GetBds_Bds50RejectedAfterDeserialization_ReturnsOnlyBds60()
        {
            var bytes = Convert.FromHexString("E519F331602401");
            ReadOnlySpan<byte> buffer = bytes;

            var result = BdsFactory.GetBds(ref buffer);

            Assert.Collection(result, candidate => Assert.IsType<Bds60>(candidate));
            Assert.Equal(0, buffer.Length);
        }

        [Fact]
        public void GetBds_Bds50AndBds60ArePlausible_ReturnsBothCandidates()
        {
            var bytes = Convert.FromHexString("FFFB23286004A7");
            ReadOnlySpan<byte> buffer = bytes;

            var result = BdsFactory.GetBds(ref buffer);

            Assert.Collection(
                result,
                candidate => Assert.IsType<Bds50>(candidate),
                candidate => Assert.IsType<Bds60>(candidate)
            );
            Assert.Equal(0, buffer.Length);
        }

        [Fact]
        public void SelectCandidate_BaseImplementation_ReturnsFirstCandidate()
        {
            var first = new Bds60();
            var candidates = new List<BdsBase> { first, new Bds50() };
            var sut = new ModeSDF21Probe();

            var result = sut.Select(candidates);

            Assert.Same(first, result);
        }

        [Fact]
        public void SelectCandidate_Df20Bds60MatchesAltitude_ReturnsBds60()
        {
            var candidates = GetAmbiguousCandidates();
            var sut = new ModeSDF20Probe { Altitude = 3000.0 / 3.28084 };

            var result = sut.Select(candidates);

            Assert.IsType<Bds60>(result);
        }

        [Fact]
        public void SelectCandidate_Df20Bds60DoesNotMatchAltitude_ReturnsBds50()
        {
            var candidates = GetAmbiguousCandidates();
            var sut = new ModeSDF20Probe { Altitude = 14000.0 / 3.28084 };

            var result = sut.Select(candidates);

            Assert.IsType<Bds50>(result);
        }

        [Fact]
        public void SelectCandidate_Df16Bds60MatchesAltitude_ReturnsBds60()
        {
            var candidates = GetAmbiguousCandidates();
            var sut = new ModeSDF16Probe { Altitude = 3000.0 / 3.28084 };

            var result = sut.Select(candidates);

            Assert.IsType<Bds60>(result);
        }

        private static List<BdsBase> GetAmbiguousCandidates()
        {
            var bytes = Convert.FromHexString("FFFB23286004A7");
            ReadOnlySpan<byte> buffer = bytes;
            return BdsFactory.GetBds(ref buffer);
        }

        private sealed class ModeSDF21Probe : ModeSDF21
        {
            public BdsBase Select(List<BdsBase> candidates)
            {
                return SelectCandidate(candidates);
            }
        }

        private sealed class ModeSDF20Probe : ModeSDF20
        {
            public BdsBase Select(List<BdsBase> candidates)
            {
                return SelectCandidate(candidates);
            }
        }

        private sealed class ModeSDF16Probe : ModeSDF16
        {
            public BdsBase Select(List<BdsBase> candidates)
            {
                return SelectCandidate(candidates);
            }
        }
    }
}
