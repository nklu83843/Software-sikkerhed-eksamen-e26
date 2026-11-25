using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace WebGoatCore.Models
{
    /// <summary>
    /// Domæneprimitiv for kreditkortnumre (AC-10 & PCI-DSS).
    /// Håndhæver de 5 valideringstrin:
    /// - Lexical: Kun cifre og bindestreger/mellemrum.
    /// - Size: 13-19 cifre (ISO/IEC 7812).
    /// - Semantic: Verifikation via Luhn-algoritmen (Mod 10).
    /// </summary>
    public sealed class CreditCardNumber : IEquatable<CreditCardNumber>
    {
        private static readonly Regex FormatRegex = new Regex(@"^[\d\s\-]{13,23}$", RegexOptions.Compiled);
        
        public string MaskedValue { get; }
        private readonly string _rawNumber; // Holdes privat jf. PCI-DSS

        public CreditCardNumber(string rawInput)
        {
            if (string.IsNullOrWhiteSpace(rawInput))
                throw new ArgumentNullException(nameof(rawInput), "Kortnummer må ikke være tomt.");

            if (!FormatRegex.IsMatch(rawInput))
                throw new ArgumentException("Ugyldigt format for kreditkortnummer.", nameof(rawInput));

            // Rens for mellemrum og bindestreger
            string digitsOnly = new string(rawInput.Where(char.IsDigit).ToArray());

            if (digitsOnly.Length < 13 || digitsOnly.Length > 19)
                throw new ArgumentOutOfRangeException(nameof(rawInput), "Kortnummer skal indeholde mellem 13 og 19 cifre.");

            if (!PassesLuhnCheck(digitsOnly))
                throw new ArgumentException("Kortnummeret fejlede Luhn-checksum kontrollen (ugyldigt kort).", nameof(rawInput));

            _rawNumber = digitsOnly;
            
            // Maskering (PCI-DSS krav: Vis kun de sidste 4 cifre)
            MaskedValue = new string('*', digitsOnly.Length - 4) + digitsOnly.Substring(digitsOnly.Length - 4);
        }

        public static bool TryParse(string input, out CreditCardNumber card, out string errorMessage)
        {
            card = null;
            errorMessage = string.Empty;

            try
            {
                card = new CreditCardNumber(input);
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
        }

        /// <summary>
        /// Validerer kortnummeret matematisk via Luhn-algoritmen (Modulus 10).
        /// Forhindrer simple tastefejl og tilfældigt genererede falske numre ved TB1.
        /// </summary>
        private static bool PassesLuhnCheck(string digits)
        {
            int sum = 0;
            bool alternate = false;
            for (int i = digits.Length - 1; i >= 0; i--)
            {
                int n = digits[i] - '0';
                if (alternate)
                {
                    n *= 2;
                    if (n > 9) n -= 9;
                }
                sum += n;
                alternate = !alternate;
            }
            return (sum % 10 == 0);
        }

        public bool Equals(CreditCardNumber other)
        {
            if (ReferenceEquals(null, other)) return false;
            return _rawNumber == other._rawNumber;
        }

        public override bool Equals(object obj) => Equals(obj as CreditCardNumber);
        public override int GetHashCode() => _rawNumber?.GetHashCode() ?? 0;
        public override string ToString() => MaskedValue; // toString returnerer ALTID den maskerede værdi!
    }
}
