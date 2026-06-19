using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Asv.Sdr
{
    /// <summary>
    /// International Morse code table for the characters that appear in radio-navigation
    /// identifiers (letters and digits). Shared by the identifier decoders.
    /// </summary>
    internal static class MorseAlphabet
    {
        public static ReadOnlyDictionary<string, char> CodeToChar { get; } =
            new(
                new Dictionary<string, char>
                {
                    { ".-", 'A' },
                    { "-...", 'B' },
                    { "-.-.", 'C' },
                    { "-..", 'D' },
                    { ".", 'E' },
                    { "..-.", 'F' },
                    { "--.", 'G' },
                    { "....", 'H' },
                    { "..", 'I' },
                    { ".---", 'J' },
                    { "-.-", 'K' },
                    { ".-..", 'L' },
                    { "--", 'M' },
                    { "-.", 'N' },
                    { "---", 'O' },
                    { ".--.", 'P' },
                    { "--.-", 'Q' },
                    { ".-.", 'R' },
                    { "...", 'S' },
                    { "-", 'T' },
                    { "..-", 'U' },
                    { "...-", 'V' },
                    { ".--", 'W' },
                    { "-..-", 'X' },
                    { "-.--", 'Y' },
                    { "--..", 'Z' },
                    { "-----", '0' },
                    { ".----", '1' },
                    { "..---", '2' },
                    { "...--", '3' },
                    { "....-", '4' },
                    { ".....", '5' },
                    { "-....", '6' },
                    { "--...", '7' },
                    { "---..", '8' },
                    { "----.", '9' },
                }
            );
    }
}
