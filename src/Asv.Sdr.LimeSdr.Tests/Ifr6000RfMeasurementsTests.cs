using System.Reflection;
using Xunit;

namespace Asv.Sdr.LimeSdr.Tests;

public sealed class Ifr6000RfMeasurementsTests
{
    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(50, 50, 0)]
    [InlineData(0, 1, 60)]
    [InlineData(99, 118, 15)]
    [InlineData(99, 199, 60)]
    [InlineData(0, 17, 251)]
    [InlineData(0, 18, 255)]
    [InlineData(0, 65535, 255)]
    [InlineData(65535, 65535, 0)]
    [InlineData(101, 100, null)]
    [InlineData(65535, 0, null)]
    public void ToPulseVariationTenthsDb_Endpoints_UsesPlusOneAndSaturatesWithoutWrapping(
        ushort min, ushort max, int? expected) =>
        Assert.Equal(expected.HasValue ? (byte?)expected.Value : null,
            Ifr6000RfMeasurements.ToPulseVariationTenthsDb(min, max));

    [Theory]
    [InlineData(0, 0, 0.0)]
    [InlineData(0, 1, 6.020599913)]
    [InlineData(99, 118, 1.510939228)]
    [InlineData(99, 199, 6.020599913)]
    [InlineData(0, 65535, 96.329598612)]
    public void ToPulseVariationDb_Endpoints_ReturnsFullPrecisionWithoutTelemetryClipping(
        ushort min, ushort max, double expected) =>
        Assert.Equal(expected, Ifr6000RfMeasurements.ToPulseVariationDb(min, max), 6);

    [Theory]
    [InlineData(0x0332, 0x0333)]
    [InlineData(0x0334, 0x0335)]
    [InlineData(0x0336, 0x0337)]
    [InlineData(0x0338, 0x0339)]
    public async Task ReadPulseVariationDbAsync_EachReplyKind_ReadsMinMaxInOneFrame(
        ushort minAddress, ushort maxAddress)
    {
        var device = DispatchProxy.Create<ILimeSdrCustomDevice, RegisterProxy>();
        var proxy = (RegisterProxy)(object)device;
        using var cancellation = new CancellationTokenSource();
        proxy.Values = [99, 118];

        var result = await Ifr6000RfMeasurements.ReadPulseVariationDbAsync(
            device, minAddress, maxAddress, cancellation.Token);

        Assert.Equal(1.510939228, result, 6);
        Assert.Equal([minAddress, maxAddress], proxy.Addresses!);
        Assert.Equal(1, proxy.FrameReads);
        Assert.Equal(0, proxy.SingleReads);
        Assert.Equal(cancellation.Token, proxy.Token);
    }

    [Theory]
    [InlineData(0x0300, -32768, 1057.232)]
    [InlineData(0x0300, -1234, 1088.766)]
    [InlineData(0x0300, 0, 1090.0)]
    [InlineData(0x0303, -1234, 1088.766)]
    [InlineData(0x0303, 1234, 1091.234)]
    [InlineData(0x0303, 32767, 1122.767)]
    public async Task ReadFrequencyMHzAsync_ReplyFamily_ConvertsAssignedSignedRegister(
        ushort address, short offsetKhz, double expected)
    {
        var device = DispatchProxy.Create<ILimeSdrCustomDevice, RegisterProxy>();
        var proxy = (RegisterProxy)(object)device;
        proxy.Values = [unchecked((ushort)offsetKhz)];

        var result = await Ifr6000RfMeasurements.ReadFrequencyMHzAsync(
            device, address, TestContext.Current.CancellationToken);

        Assert.Equal(expected, result, 6);
        Assert.Equal([address], proxy.Addresses!);
        Assert.Equal(1, proxy.SingleReads);
        Assert.Equal(0, proxy.FrameReads);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReadAsync_Cancellation_IsPropagatedWithoutMeasurements(bool frequency)
    {
        var device = DispatchProxy.Create<ILimeSdrCustomDevice, RegisterProxy>();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => frequency
            ? Ifr6000RfMeasurements.ReadFrequencyMHzAsync(device, 0x0300, cancellation.Token)
            : Ifr6000RfMeasurements.ReadPulseVariationDbAsync(device, 0x0332, 0x0333, cancellation.Token));
    }

    [Fact]
    public async Task ReadPulseVariationDbAsync_InvalidRange_ReturnsUnavailable()
    {
        var device = DispatchProxy.Create<ILimeSdrCustomDevice, RegisterProxy>();
        ((RegisterProxy)(object)device).Values = [101, 100];

        var result = await Ifr6000RfMeasurements.ReadPulseVariationDbAsync(
            device, 0x0332, 0x0333, TestContext.Current.CancellationToken);

        Assert.True(double.IsNaN(result));
    }

    public class RegisterProxy : DispatchProxy
    {
        public ushort[] Values { get; set; } = [];
        public ushort[]? Addresses { get; private set; }
        public CancellationToken Token { get; private set; }
        public int FrameReads { get; private set; }
        public int SingleReads { get; private set; }

        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            Token = (CancellationToken)args![1]!;
            if (method!.Name == nameof(ILimeSdrCustomDevice.ReadCustomRegistersFrame))
            {
                FrameReads++;
                Addresses = (ushort[])args[0]!;
                return Token.IsCancellationRequested
                    ? Task.FromCanceled<ushort[]>(Token)
                    : Task.FromResult(Values);
            }
            if (method.Name == nameof(ILimeSdrCustomDevice.ReadCustomRegister))
            {
                SingleReads++;
                Addresses = [(ushort)args[0]!];
                return Token.IsCancellationRequested
                    ? Task.FromCanceled<ushort>(Token)
                    : Task.FromResult(Values[0]);
            }
            throw new NotSupportedException(method.Name);
        }
    }
}
