using Asv.Sdr.LimeSdr;
using Xunit;

namespace Asv.Sdr.LimeSdr.Tests;

public class Ifr6000ModeAcSnapshotTests
{
    // Delay words encode signed 0.025 us ticks directly: -20 ticks for A and
    // +40 ticks for C. Only the explicit host calibration offset is added.
    private static ushort[] Frame() => [0x2A7F, 0, 0xC8C7, 0x1213, 0x1415, 0xFF02, 0xFFEC, 3, 40, 2];

    [Fact]
    public void TryDecode_StableReadyGeneration_DecodesOneCalibratedBatch()
    {
        Assert.True(Ifr6000ModeAcSnapshot.TryDecode(0xAAFF, Frame(), 0xAAFF, 1.25, out var sample, out var reason));
        Assert.NotNull(sample);
        Assert.True(sample.IsCoherent);
        Assert.Equal((byte)255, sample.Generation);
        Assert.Null(sample.Diagnostics);
        Assert.Equal("accepted", reason);
        Assert.Equal(("0777", true), sample.ModeA);
        Assert.Equal(100f, sample.ReplyRatio.ModeA);
        Assert.Equal(99.5f, sample.ReplyRatio.ModeC);
        Assert.Equal(0.45f, sample.ModeAPulseWidth.F1, 5);
        Assert.Equal(0.475f, sample.ModeAPulseWidth.F2, 5);
        Assert.Equal(20.275f, sample.PulseSpacing.ModeA, 5);
        Assert.Equal(20.35f, sample.PulseSpacing.ModeC, 5);
        Assert.Equal(0.75f, sample.ModeAReplyDelay, 5);
        Assert.Equal(2.25f, sample.ModeCReplyDelay, 5);
        Assert.Equal(0.075f, sample.ModeAReplyJitter, 5);
        Assert.Equal(0.05f, sample.ModeCReplyJitter, 5);
    }

    [Theory]
    [InlineData(0x0000, 20.3f, 20.3f)]
    [InlineData(0xFF02, 20.275f, 20.35f)]
    [InlineData(0x02FF, 20.35f, 20.275f)]
    [InlineData(0x807F, 17.1f, 23.475f)]
    [InlineData(0x7F80, 23.475f, 17.1f)]
    [InlineData(0xFB05, 20.175f, 20.425f)]
    [InlineData(0xFC04, 20.2f, 20.4f)]
    [InlineData(0xFD03, 20.225f, 20.375f)]
    [InlineData(0x04FC, 20.4f, 20.2f)]
    [InlineData(0x05FB, 20.425f, 20.175f)]
    public void Decode_SignedSpacingOffsets_ReturnsFullF1F2Interval(
        ushort spacingWord, float expectedModeAUs, float expectedModeCUs)
    {
        var words = Frame();
        words[5] = spacingWord;

        Assert.True(Ifr6000ModeAcSnapshot.TryDecode(
            0xAA01, words, 0xAA01, 1.25, out var coherent, out _));
        var legacy = Ifr6000ModeAcSnapshot.DecodeLegacy(words, 1.25);

        Assert.NotNull(coherent);

        // F1-F2 is the full interval: 20.3 us plus a signed 0.025 us offset.
        // Reply-delay calibration must not shift this pulse-to-pulse interval.
        foreach (var sample in new[] { coherent, legacy })
        {
            Assert.Equal(expectedModeAUs, sample.PulseSpacing.ModeA, 5);
            Assert.Equal(expectedModeCUs, sample.PulseSpacing.ModeC, 5);
        }
    }

    [Fact]
    public void Decode_ZeroRawDelayAndZeroOffset_DoesNotAddNominalThreeMicroseconds()
    {
        var words = Frame();
        words[6] = 0;
        words[8] = 0;

        Assert.True(Ifr6000ModeAcSnapshot.TryDecode(
            0xAA01, words, 0xAA01, 0, out var coherent, out _));
        var legacy = Ifr6000ModeAcSnapshot.DecodeLegacy(words, 0);

        Assert.NotNull(coherent);
        foreach (var sample in new[] { coherent, legacy })
        {
            Assert.Equal(0f, sample.ModeAReplyDelay);
            Assert.Equal(0f, sample.ModeCReplyDelay);
        }
    }

    [Fact]
    public void Diagnostics_KnownSignature_PreservesAllSaturatingCounters()
    {
        ushort[] words = [0xAC01, 1, 2, 3, 4, ushort.MaxValue, 6];

        var diagnostics = Ifr6000ModeAcDiagnostics.Decode(words);

        Assert.NotNull(diagnostics);
        Assert.Equal((ushort)0xAC01, diagnostics.SchemaSignature);
        Assert.Equal((ushort)1, diagnostics.RejectedModeACount);
        Assert.Equal((ushort)2, diagnostics.RejectedModeCCount);
        Assert.Equal((ushort)3, diagnostics.AbortedModeACount);
        Assert.Equal((ushort)4, diagnostics.AbortedModeCCount);
        Assert.Equal(ushort.MaxValue, diagnostics.OverrunCount);
        Assert.Equal((ushort)6, diagnostics.ConservativeIntervalAssignmentCount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(0xAC00)]
    [InlineData(0xAC02)]
    public void Diagnostics_UnsupportedSignature_IsNotExposed(ushort signature)
    {
        ushort[] words = [signature, 1, 2, 3, 4, 5, 6];

        Assert.Null(Ifr6000ModeAcDiagnostics.Decode(words));
    }

    [Fact]
    public void Diagnostics_MissingRegister_IsNotExposed()
    {
        ushort[] words = [0xAC01, 1, 2, 3, 4, 5];

        Assert.Null(Ifr6000ModeAcDiagnostics.Decode(words));
    }

    [Fact]
    public void TryDecode_CoherentGeneration_AttachesMatchingDiagnostics()
    {
        var diagnostics = Ifr6000ModeAcDiagnostics.Decode([0xAC01, 10, 20, 30, 40, 50, 60]);

        Assert.True(Ifr6000ModeAcSnapshot.TryDecode(
            0xAA2A, Frame(), 0xAA2A, 0, out var sample, out var reason, diagnostics));

        Assert.Equal("accepted", reason);
        Assert.Same(diagnostics, sample!.Diagnostics);
        Assert.Equal((byte)42, sample.Generation);
    }

    [Fact]
    public void TryDecode_ChangedGeneration_DoesNotExposeDiagnosticsAsCoherent()
    {
        var diagnostics = Ifr6000ModeAcDiagnostics.Decode([0xAC01, 10, 20, 30, 40, 50, 60]);

        Assert.False(Ifr6000ModeAcSnapshot.TryDecode(
            0xAA29, Frame(), 0xAA2A, 0, out var sample, out var reason, diagnostics));

        Assert.Equal("generation-changed", reason);
        Assert.Null(sample);
    }

    [Theory]
    [InlineData(0xAA01, 0xAA02, "generation-changed")]
    [InlineData(0xAAFF, 0xAA00, "generation-changed")]
    [InlineData(0x2A01, 0x2A01, "not-ready")]
    [InlineData(0x2A01, 0xAA01, "not-ready")]
    [InlineData(0, 0, "unsupported")]
    [InlineData(0xAB01, 0xAB01, "unsupported")]
    public void TryDecode_UnusableStatus_RejectsSnapshot(ushort before, ushort after, string expected)
    {
        Assert.False(Ifr6000ModeAcSnapshot.TryDecode(before, Frame(), after, 0, out var sample, out var reason));
        Assert.Null(sample);
        Assert.Equal(expected, reason);
    }

    [Fact]
    public void TryDecode_MissingWord_RejectsSnapshot()
    {
        Assert.False(Ifr6000ModeAcSnapshot.TryDecode(0xAA01, Frame()[..9], 0xAA01, 0, out _, out var reason));
        Assert.Equal("register-count", reason);
    }

    [Fact]
    public void TryDecode_ZeroReplies_HasNoTimingMeasurement()
    {
        var words = Frame();
        words[2] = 0;
        Assert.True(Ifr6000ModeAcSnapshot.TryDecode(0xAA01, words, 0xAA01, 0, out var sample, out _));
        Assert.True(float.IsNaN(sample!.ModeAReplyDelay));
        Assert.True(float.IsNaN(sample.ModeAReplyJitter));
        Assert.True(float.IsNaN(sample.ModeCReplyDelay));
        Assert.True(float.IsNaN(sample.ModeCReplyJitter));
        Assert.True(float.IsNaN(sample.ModeAPulseWidth.F1));
        Assert.True(float.IsNaN(sample.ModeCPulseWidth.F2));
        Assert.Equal((0f, 0f), sample.ReplyRatio);
    }

    [Theory]
    [InlineData(0x01C8, true)]
    [InlineData(0xC801, false)]
    public void TryDecode_OneReply_KeepsDelayButHasNoJitter(ushort rawCounts, bool modeAHasOneReply)
    {
        var words = Frame();
        words[2] = rawCounts;

        Assert.True(Ifr6000ModeAcSnapshot.TryDecode(0xAA01, words, 0xAA01, 0, out var sample, out _));
        Assert.NotNull(sample);

        if (modeAHasOneReply)
        {
            Assert.Equal(0.5f, sample.ReplyRatio.ModeA);
            Assert.Equal(-0.5f, sample.ModeAReplyDelay, 5);
            Assert.True(float.IsNaN(sample.ModeAReplyJitter));
            Assert.False(float.IsNaN(sample.ModeCReplyJitter));
        }
        else
        {
            Assert.Equal(0.5f, sample.ReplyRatio.ModeC);
            Assert.Equal(1f, sample.ModeCReplyDelay, 5);
            Assert.True(float.IsNaN(sample.ModeCReplyJitter));
            Assert.False(float.IsNaN(sample.ModeAReplyJitter));
        }
    }

    [Fact]
    public void TryDecode_ImpossibleCounts_PreservesEvidenceWithoutClamping()
    {
        var words = Frame();
        words[2] = 0xC9FF;
        Assert.True(Ifr6000ModeAcSnapshot.TryDecode(0xAA01, words, 0xAA01, 0, out var sample, out var reason));
        Assert.Equal((100.5f, 127.5f), sample!.ReplyRatio);
        Assert.Equal("count-out-of-range", reason);
    }

    [Fact]
    public void DecodeLegacy_CalibratedRegisters_PreservesValuesWithoutClaimingAtomicity()
    {
        var sample = Ifr6000ModeAcSnapshot.DecodeLegacy(Frame(), 1.25);

        Assert.False(sample.IsCoherent);
        Assert.Null(sample.Generation);
        Assert.Null(sample.Diagnostics);
        Assert.Equal(("0777", true), sample.ModeA);
        Assert.Equal((100f, 99.5f), sample.ReplyRatio);
        Assert.Equal(0.45f, sample.ModeAPulseWidth.F1, 5);
        Assert.Equal(0.475f, sample.ModeAPulseWidth.F2, 5);
        Assert.Equal(0.5f, sample.ModeCPulseWidth.F1, 5);
        Assert.Equal(0.525f, sample.ModeCPulseWidth.F2, 5);
        Assert.Equal(20.275f, sample.PulseSpacing.ModeA, 5);
        Assert.Equal(20.35f, sample.PulseSpacing.ModeC, 5);
        Assert.Equal(0.75f, sample.ModeAReplyDelay, 5);
        Assert.Equal(2.25f, sample.ModeCReplyDelay, 5);
        Assert.Equal(0.075f, sample.ModeAReplyJitter, 5);
        Assert.Equal(0.05f, sample.ModeCReplyJitter, 5);
    }

    [Fact]
    public void DecodeLegacy_ImpossibleCounts_PreservesEvidenceWithoutClamping()
    {
        var words = Frame();
        words[2] = 0xC9FF;

        var sample = Ifr6000ModeAcSnapshot.DecodeLegacy(words, 0);

        Assert.Equal((100.5f, 127.5f), sample.ReplyRatio);
        Assert.False(sample.IsCoherent);
    }

    [Theory]
    [InlineData(9)]
    [InlineData(11)]
    public void DecodeLegacy_IncorrectRegisterCount_RejectsSnapshot(int count)
    {
        Assert.Throws<ArgumentException>(() => Ifr6000ModeAcSnapshot.DecodeLegacy(new ushort[count], 0));
    }

    [Fact]
    public void DecodeLegacy_OldFirmware_DoesNotClaimAtomicityOrHideExistingOutlier()
    {
        var words = Frame();
        words[7] = 7719;
        var sample = Ifr6000ModeAcSnapshot.DecodeLegacy(words, 0);
        Assert.False(sample.IsCoherent);
        Assert.Null(sample.Generation);
        Assert.Null(sample.Diagnostics);
        Assert.Equal(192.975f, sample.ModeAReplyJitter, 3);
    }

    [Fact]
    public void DecodeLegacy_ZeroReplies_DoesNotInventZeroJitter()
    {
        var words = Frame();
        words[2] = 0;
        var sample = Ifr6000ModeAcSnapshot.DecodeLegacy(words, 0);
        Assert.True(float.IsNaN(sample.ModeAReplyJitter));
        Assert.True(float.IsNaN(sample.ModeCReplyDelay));
    }

    [Fact]
    public void DecodeLegacy_OneReply_KeepsDelayButHasNoJitter()
    {
        var words = Frame();
        words[2] = 0x01C8;

        var sample = Ifr6000ModeAcSnapshot.DecodeLegacy(words, 0);

        Assert.Equal(-0.5f, sample.ModeAReplyDelay, 5);
        Assert.True(float.IsNaN(sample.ModeAReplyJitter));
        Assert.False(float.IsNaN(sample.ModeCReplyJitter));
    }

    [Fact]
    public void TryDecode_CustomBatchSize_UsesProfileAndPreservesCalibration()
    {
        var words = Frame();
        words[2] = 0x3231; // 50 Mode A replies and 49 Mode C replies.
        var profile = new Ifr6000MeasurementProfile { BatchSize = 50 };

        Assert.True(Ifr6000ModeAcSnapshot.TryDecode(
            0xAA01, words, 0xAA01, 1.25, out var sample, out var reason, profile: profile));

        Assert.NotNull(sample);
        Assert.Equal("accepted", reason);
        Assert.True(sample.IsCoherent);
        Assert.Equal((100f, 98f), sample.ReplyRatio);
        Assert.Equal(0.75f, sample.ModeAReplyDelay, 5);
        Assert.Equal(2.25f, sample.ModeCReplyDelay, 5);
        Assert.Equal(0.075f, sample.ModeAReplyJitter, 5);
        Assert.Equal(0.05f, sample.ModeCReplyJitter, 5);
    }

    [Fact]
    public void DecodeLegacy_CustomBatchSize_UsesProfileWithoutClaimingAtomicity()
    {
        var words = Frame();
        words[2] = 0x3231;
        var profile = new Ifr6000MeasurementProfile { BatchSize = 50 };

        var sample = Ifr6000ModeAcSnapshot.DecodeLegacy(words, 1.25, profile);

        Assert.Equal((100f, 98f), sample.ReplyRatio);
        Assert.False(sample.IsCoherent);
        Assert.Null(sample.Generation);
        Assert.Null(sample.Diagnostics);
        Assert.Equal(("0777", true), sample.ModeA);
        Assert.Equal(0.75f, sample.ModeAReplyDelay, 5);
        Assert.Equal(2.25f, sample.ModeCReplyDelay, 5);
        Assert.Equal(0.45f, sample.ModeAPulseWidth.F1, 5);
        Assert.Equal(20.275f, sample.PulseSpacing.ModeA, 5);
        Assert.Equal(20.35f, sample.PulseSpacing.ModeC, 5);
    }

    [Theory]
    [InlineData(0x3332, 102f, 100f)]
    [InlineData(0x3233, 100f, 102f)]
    public void Decode_CustomBatchExceeded_PreservesImpossibleRatios(
        ushort rawCounts, float modeA, float modeC)
    {
        var words = Frame();
        words[2] = rawCounts;
        var profile = new Ifr6000MeasurementProfile { BatchSize = 50 };

        Assert.True(Ifr6000ModeAcSnapshot.TryDecode(
            0xAA01, words, 0xAA01, 0, out var coherent, out var reason, profile: profile));
        var legacy = Ifr6000ModeAcSnapshot.DecodeLegacy(words, 0, profile);

        Assert.Equal("count-out-of-range", reason);
        Assert.Equal((modeA, modeC), coherent!.ReplyRatio);
        Assert.Equal((modeA, modeC), legacy.ReplyRatio);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Decode_CustomBatchWithTooFewReplies_UsesRawCountForTimingValidity(byte count)
    {
        var words = Frame();
        words[2] = (ushort)((count << 8) | count);
        var profile = new Ifr6000MeasurementProfile { BatchSize = 50 };

        Assert.True(Ifr6000ModeAcSnapshot.TryDecode(
            0xAA01, words, 0xAA01, 0, out var coherent, out var reason, profile: profile));
        var legacy = Ifr6000ModeAcSnapshot.DecodeLegacy(words, 0, profile);

        Assert.Equal("accepted", reason);
        foreach (var sample in new[] { coherent!, legacy })
        {
            Assert.Equal((count * 2f, count * 2f), sample.ReplyRatio);
            Assert.True(float.IsNaN(sample.ModeAReplyJitter));
            Assert.True(float.IsNaN(sample.ModeCReplyJitter));
            if (count == 0)
            {
                Assert.True(float.IsNaN(sample.ModeAReplyDelay));
                Assert.True(float.IsNaN(sample.ModeCReplyDelay));
                Assert.True(float.IsNaN(sample.ModeAPulseWidth.F1));
                Assert.True(float.IsNaN(sample.ModeCPulseWidth.F1));
                Assert.True(float.IsNaN(sample.PulseSpacing.ModeA));
                Assert.True(float.IsNaN(sample.PulseSpacing.ModeC));
            }
            else
            {
                Assert.Equal(-0.5f, sample.ModeAReplyDelay, 5);
                Assert.Equal(1f, sample.ModeCReplyDelay, 5);
                Assert.Equal(0.45f, sample.ModeAPulseWidth.F1, 5);
                Assert.Equal(0.5f, sample.ModeCPulseWidth.F1, 5);
            }
        }
    }
}
