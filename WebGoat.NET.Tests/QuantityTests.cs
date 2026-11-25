using System;
using WebGoatCore.Models;
using Xunit;

namespace WebGoat.NET.Tests.Domain
{
    public class QuantityTests
    {
        // =========================================================================
        // KATEGORI 1 & 2: Normal Behavior & Boundary Behavior Testing
        // =========================================================================

        [Fact]
        public void Constructor_WithValidNormalValue_CreatesInstanceSuccessfully()
        {
            // Arrange
            int validAmount = 5;

            // Act
            var quantity = new Quantity(validAmount);

            // Assert
            Assert.NotNull(quantity);
            Assert.Equal(validAmount, quantity.Value);
        }

        [Theory]
        [InlineData(1)]   // Nedre semantiske grænseværdi (MinValue)
        [InlineData(2)]
        [InlineData(100)]
        [InlineData(240)] // Øvre semantiske grænseværdi (MaxValue)
        public void Constructor_WithBoundaryValues_CreatesInstanceWithoutExceptions(int boundaryValue)
        {
            // Act & Assert (Verificerer at gyldige grænser accepteres)
            var exception = Record.Exception(() => new Quantity(boundaryValue));
            Assert.Null(exception);
        }

        // =========================================================================
        // KATEGORI 3: Invalid Input Testing (Negativ Test / Fail-Fast mod Anti-Hamlet)
        // =========================================================================

        [Theory]
        [InlineData(0)]   // Nulstillet vareantal
        [InlineData(-1)]  // Simpelt negativt tal
        [InlineData(-2)]  // Anti-Hamlet angrebsvektor fra AC-08
        [InlineData(-50)]
        [InlineData(241)] // Overskridelse af tilladt maksimum
        public void Constructor_WithInvalidValues_ThrowsArgumentOutOfRangeException(int invalidValue)
        {
            // Act & Assert (Verificerer aggressiv Fail-Fast ved ulovligt forretningsinput)
            Assert.Throws<ArgumentOutOfRangeException>(() => new Quantity(invalidValue));
        }

        // =========================================================================
        // KATEGORI 4: Extreme Input Testing (DoS og Integer Overflow Håndtering)
        // =========================================================================

        [Theory]
        [InlineData(int.MinValue)] // -2.147.483.648 (Heltals-underflow vektor)
        [InlineData(int.MaxValue)] // +2.147.483.647 (Heltals-overflow vektor)
        public void Constructor_WithExtremeIntegerValues_ThrowsArgumentOutOfRangeException(int extremeValue)
        {
            // Act & Assert
            Assert.Throws<ArgumentOutOfRangeException>(() => new Quantity(extremeValue));
        }

        // =========================================================================
        // Test af TryParse Sikkerhedsmetode
        // =========================================================================

        [Fact]
        public void TryParse_WithNegativeString_ReturnsFalseAndErrorMessage()
        {
            // Act
            bool result = Quantity.TryParse("-5", out var quantity, out var error);

            // Assert
            Assert.False(result);
            Assert.Null(quantity);
            Assert.Contains("skal være mellem", error);
        }
    }
}