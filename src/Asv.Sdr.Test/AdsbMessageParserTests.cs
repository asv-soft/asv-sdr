using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using Xunit;

namespace Asv.Sdr.Test
{
    public class AdsbMessageParserTests
    {
        // Publicly documented DF17 airborne-position example.
        private static readonly byte[] ValidDf17Frame = Convert.FromHexString(
            "8D40621D58C382D690C8AC2863A7"
        );

        private static readonly byte[] ValidDf18Cf0Frame = Convert.FromHexString(
            "9040621D58C382D690C8AC556F52"
        );

        [Fact]
        public void ProcessSample_ValidDf17Frame_PublishesMessageAndCorrectDfDiagnostic()
        {
            using var parser = new AdsbMessageParser();
            parser.RegisterDefaultMessages();
            var messages = new List<AdsbDfMessageBase>();
            var diagnostics = new List<string>();
            var errors = new List<AdsbParserException>();
            using var messageSubscription = parser.OnMessage.Subscribe(messages.Add);
            using var diagnosticSubscription = parser.OnMessageRecev.Subscribe(diagnostics.Add);
            using var errorSubscription = parser.OnError.Subscribe(errors.Add);

            var completed = ProcessFrame(parser, ValidDf17Frame);

            Assert.True(completed);
            Assert.Single(messages);
            Assert.IsType<AdsbAirbornePositionWithBaroAlt>(messages[0]);
            Assert.Contains("Down link format: 17", diagnostics);
            Assert.DoesNotContain(errors, error => error is AdsbCrcErrorException);
        }

        [Fact]
        public void ProcessSample_ValidDf18Cf0Frame_PublishesMessageAndCorrectDfDiagnostic()
        {
            using var parser = new AdsbMessageParser();
            parser.RegisterDefaultMessages();
            var messages = new List<AdsbDfMessageBase>();
            var diagnostics = new List<string>();
            var errors = new List<AdsbParserException>();
            using var messageSubscription = parser.OnMessage.Subscribe(messages.Add);
            using var diagnosticSubscription = parser.OnMessageRecev.Subscribe(diagnostics.Add);
            using var errorSubscription = parser.OnError.Subscribe(errors.Add);

            var completed = ProcessFrame(parser, ValidDf18Cf0Frame);

            Assert.True(completed);
            var message = Assert.IsType<AdsbAirbornePositionWithBaroAlt>(Assert.Single(messages));
            Assert.Equal(18, message.DownlinkFormat);
            Assert.Contains("Down link format: 18", diagnostics);
            Assert.DoesNotContain(errors, error => error is AdsbCrcErrorException);
        }

        [Fact]
        public void ProcessSample_CorruptedDf17Frame_RejectsMessageWithCrcError()
        {
            using var parser = new AdsbMessageParser();
            parser.RegisterDefaultMessages();
            var messages = new List<AdsbDfMessageBase>();
            var errors = new List<AdsbParserException>();
            using var messageSubscription = parser.OnMessage.Subscribe(messages.Add);
            using var errorSubscription = parser.OnError.Subscribe(errors.Add);
            var corrupted = (byte[])ValidDf17Frame.Clone();
            corrupted[6] ^= 0x01;

            var completed = ProcessFrame(parser, corrupted);

            Assert.False(completed);
            Assert.Empty(messages);
            Assert.Contains(errors, error => error is AdsbCrcErrorException);
        }

        private static bool ProcessFrame(AdsbMessageParser parser, IReadOnlyList<byte> frame)
        {
            foreach (var preambleByte in TransponderHelper.Preamble)
            {
                for (var bit = 7; bit >= 0; bit--)
                {
                    parser.ProcessSample((byte)((preambleByte >> bit) & 0x01));
                }
            }

            var completed = false;
            foreach (var value in frame)
            {
                for (var bit = 7; bit >= 0; bit--)
                {
                    var decodedBit = (value >> bit) & 0x01;
                    completed |= parser.ProcessSample((byte)decodedBit);
                    completed |= parser.ProcessSample((byte)(decodedBit ^ 0x01));
                }
            }

            return completed;
        }
    }
}
