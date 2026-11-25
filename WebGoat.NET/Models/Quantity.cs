using System;

namespace WebGoatCore.Models
{
    /// <summary>
    /// Domæneprimitiv der repræsenterer et valideret vareantal.
    /// Håndhæver forretningsmæssig invarians: Et vareantal skal være et positivt heltal mellem 1 og 240.
    /// Garanterer "Validity by Existence" - et ugyldigt Quantity-objekt kan aldrig eksistere i RAM.
    /// </summary>
    public sealed class Quantity : IEquatable<Quantity>, IComparable<Quantity>
    {
        public const int MinValue = 1;
        public const int MaxValue = 240;

        public int Value { get; }

        public Quantity(int value)
        {
            // Håndhævelse af nedre grænse (Fail-Fast ved 0 eller negative tal)
            if (value < MinValue)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    $"Vareantal skal være mindst {MinValue}. Værdier mindre end eller lig med 0 er ikke tilladt.");
            }

            // Håndhævelse af øvre forretningsmæssig grænse (forhindrer DoS og urealistiske ordrer)
            if (value > MaxValue)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    $"Vareantal kan maksimalt udgøre {MaxValue} enheder pr. varelinje.");
            }

            Value = value;
        }

        // Implicit konvertering til int for problemfri beregning
        public static implicit operator int(Quantity quantity) => quantity.Value;

        // Eksplicit factory-metode til parsing af strenge fra HTTP-input
        public static Quantity Parse(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                throw new ArgumentException("Input må ikke være tomt eller null.", nameof(input));
            }

            if (!int.TryParse(input.Trim(), out var parsedValue))
            {
                throw new ArgumentException("Input skal være et gyldigt heltal.", nameof(input));
            }

            return new Quantity(parsedValue);
        }

        // TryParse mønster til sikker parsing i controllers uden exceptions i kontrolflowet
        public static bool TryParse(string input, out Quantity quantity, out string errorMessage)
        {
            quantity = null;
            errorMessage = string.Empty;

            if (string.IsNullOrWhiteSpace(input))
            {
                errorMessage = "Vareantal skal angives.";
                return false;
            }

            if (!int.TryParse(input.Trim(), out var parsedValue))
            {
                errorMessage = "Vareantal skal være et numerisk heltal.";
                return false;
            }

            if (parsedValue < MinValue || parsedValue > MaxValue)
            {
                errorMessage = $"Vareantal skal være mellem {MinValue} og {MaxValue}.";
                return false;
            }

            quantity = new Quantity(parsedValue);
            return true;
        }

        #region Value Object Equality
        public bool Equals(Quantity other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Value == other.Value;
        }

        public override bool Equals(object obj) => Equals(obj as Quantity);

        public override int GetHashCode() => Value.GetHashCode();

        public int CompareTo(Quantity other)
        {
            if (other == null) return 1;
            return Value.CompareTo(other.Value);
        }

        public override string ToString() => Value.ToString();

        public static bool operator ==(Quantity left, Quantity right) => Equals(left, right);
        public static bool operator !=(Quantity left, Quantity right) => !Equals(left, right);
        #endregion
    }
}