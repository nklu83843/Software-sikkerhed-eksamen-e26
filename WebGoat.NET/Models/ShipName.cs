using System;
using System.Text.RegularExpressions;

namespace WebGoatCore.Models
{
    /// <summary>
    /// 
    /// Domæneprimitiv der repræsenterer et verificeret forsendelsesmodtagernavn (ShipName).
    /// Håndhæver princippet om "Validity by Existence" gennem de 5 valideringstrin.
    /// Blokerer SQL Injection, XSS og kontroltegn direkte ved Tillidsgrænse 1.
    /// 
    /// De 5 Valideringstrin for ShipName
 
    ///    1. Origin (Oprindelse): Tjek for null og tomme referencer.

    ///    2. Size (Størrelse): Begrænsning af tilladt tekstlængde (afviser buffer-overløb og lange injection-strenge). 
    ///        Længdekontrol udføres altid før regulære udtryk for at eliminere risikoen for ReDoS (Regular Expression Denial of Service).

    ///    3. Lexical (Leksikalsk struktur): Kontrol af det tilladte tegnsæt via en positiv godkendelsesliste (Allow-list).

    ///    4. Syntax (Syntaks / Mønster): Kontrol af sammensætning (f.eks. forbud mod udelukkende mellemrum eller starttegn med bindestreg).

    ///    5. Semantics (Semantik): Kontekstuel forretningsvalidering (håndteres af de overordnede entiteter).
    /// </summary>
    public sealed class ShipName : IEquatable<ShipName>
    {
        public const int MinLength = 2;
        public const int MaxLength = 40;

        // Præ-kompileret Allow-list Regex: Kun bogstaver (inkl. danske), mellemrum og bindestreger
        private static readonly Regex ValidNamePattern = new Regex(
            @"^[a-zA-ZæøåÆØÅ]+([ \-][a-zA-ZæøåÆØÅ]+)*$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public string Value { get; }

        public ShipName(string value)
        {
            // Trin 1: Origin (Null & Whitespace check)
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentNullException(nameof(value), "Modtagernavn må ikke være tomt eller null.");
            }

            var trimmedValue = value.Trim();

            // Trin 2: Size check (Længdevalidering: 2 til 40 tegn)
            // Længdetjek udføres ALTID FØR regex for at forhindre ReDoS (RegEx Denial of Service)
            if (trimmedValue.Length < MinLength || trimmedValue.Length > MaxLength)
            {
                throw new ArgumentException(
                    $"Modtagernavn skal have en længde mellem {MinLength} og {MaxLength} tegn. Angivet længde: {trimmedValue.Length}.",
                    nameof(value));
            }

            // Trin 3 & 4: Lexical & Syntax check via positiv Allow-list
            if (!ValidNamePattern.IsMatch(trimmedValue))
            {
                throw new ArgumentException(
                    "Modtagernavn indeholder ugyldige tegn. Kun alfabetiske tegn, mellemrum og bindestreg er tilladt.",
                    nameof(value));
            }

            Value = trimmedValue;
        }

        // TryParse til brug ved modelbinding og i controllers
        public static bool TryParse(string input, out ShipName shipName, out string errorMessage)
        {
            shipName = null;
            errorMessage = string.Empty;

            if (string.IsNullOrWhiteSpace(input))
            {
                errorMessage = "Modtagernavn skal udfyldes.";
                return false;
            }

            var trimmed = input.Trim();
            if (trimmed.Length < MinLength || trimmed.Length > MaxLength)
            {
                errorMessage = $"Modtagernavn skal være mellem {MinLength} og {MaxLength} tegn.";
                return false;
            }

            if (!ValidNamePattern.IsMatch(trimmed))
            {
                errorMessage = "Modtagernavn må kun indeholde bogstaver, bindestreg og mellemrum.";
                return false;
            }

            shipName = new ShipName(trimmed);
            return true;
        }

        public static implicit operator string(ShipName shipName) => shipName?.Value;

        #region Value Object Equality
        public bool Equals(ShipName other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return string.Equals(Value, other.Value, StringComparison.OrdinalIgnoreCase);
        }

        public override bool Equals(object obj) => Equals(obj as ShipName);

        public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(Value);

        public override string ToString() => Value;

        public static bool operator ==(ShipName left, ShipName right) => Equals(left, right);
        public static bool operator !=(ShipName left, ShipName right) => !Equals(left, right);
        #endregion
    }
}