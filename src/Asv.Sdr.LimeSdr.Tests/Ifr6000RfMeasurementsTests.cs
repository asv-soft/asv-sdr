using System.Reflection;
using Xunit;

namespace Asv.Sdr.LimeSdr.Tests;

public sealed class Ifr6000RfMeasurementsTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(3, 2)]
    [InlineData(4, 3)]
    [InlineData(24, 15)]
    [InlineData(32, 20)]
    [InlineData(33, 21)]
    [InlineData(96, 60)]
    [InlineData(404, 253)]
    [InlineData(405, 253)]
    [InlineData(406, 254)]
    [InlineData(407, 254)]
    [InlineData(408, 255)]
    [InlineData(65535, 255)]
    public void ToPulseVariationTenthsDb_Q12_4_RoundsOnceAndSaturates(ushort value, byte expected) =>
        Assert.Equal(expected, Ifr6000RfMeasurements.ToPulseVariationTenthsDb(value));

    [Theory]
    [InlineData(0, 0.0)]
    [InlineData(1, 0.0625)]
    [InlineData(4, 0.25)]
    [InlineData(16, 1.0)]
    [InlineData(24, 1.5)]
    [InlineData(31, 1.9375)]
    [InlineData(32, 2.0)]
    [InlineData(33, 2.0625)]
    [InlineData(48, 3.0)]
    [InlineData(96, 6.0)]
    [InlineData(192, 12.0)]
    [InlineData(408, 25.5)]
    [InlineData(65535, 4095.9375)]
    public void ToPulseVariationDb_Q12_4_PreservesFullPrecision(ushort value, double expected) =>
        Assert.Equal(expected, Ifr6000RfMeasurements.ToPulseVariationDb(value));

    [Theory]
    [InlineData(0x0333, 33, 2.0625)]
    [InlineData(0x0334, 33, 2.0625)]
    [InlineData(0x0335, 0, 0.0)]
    [InlineData(0x0335, 24, 1.5)]
    [InlineData(0x0335, 65535, 4095.9375)]
    public async Task ReadPulseVariationDbAsync_AssignedRegister_ReadsActualValueIncludingZero(
        ushort address, ushort raw, double expected)
    {
        var device = DispatchProxy.Create<ILimeSdrCustomDevice, RegisterProxy>();
        var proxy = (RegisterProxy)(object)device;
        using var cancellation = new CancellationTokenSource();
        proxy.Values = [raw];

        var result = await Ifr6000RfMeasurements.ReadPulseVariationDbAsync(
            device, address, cancellation.Token);

        Assert.Equal(expected, result);
        Assert.Equal([address], proxy.Addresses!);
        Assert.Equal(0, proxy.FrameReads);
        Assert.Equal(1, proxy.SingleReads);
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
            : Ifr6000RfMeasurements.ReadPulseVariationDbAsync(device, 0x0333, cancellation.Token));
    }

    [Fact]
    public void ModeSPulseVariationApi_ExposesOneReaderAndAssignedRegister()
    {
        foreach (var type in new[] { typeof(ILimeSdrIfr6000Device), typeof(LimeSdrIfr6000Device) })
        {
            var reader = Assert.Single(type.GetMethods(), method =>
                method.Name.StartsWith("ReadModeS", StringComparison.Ordinal)
                && method.Name.Contains("PulseVariation", StringComparison.Ordinal));
            Assert.Equal("ReadModeSPulseVariationDb", reader.Name);
            Assert.Equal(typeof(Task<double>), reader.ReturnType);
            Assert.Empty(reader.GetParameters());
        }
        var register = typeof(LimeSdrIfr6000Device)
            .GetField("RatioAmpDbS_15_0", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(register);
        Assert.Equal((ushort)0x0335, register.GetRawConstantValue());
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
