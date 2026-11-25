using System;
using WebGoatCore.Models;
using Xunit;

namespace WebGoat.NET.Tests.Domain
{
    public class CreditCardNumberTests
    {
        // =========================================================================
        // KATEGORI 1 & 2: Normal Behavior & Boundary Testing (Gyldige Luhn-kort)
        // =========================================================================

        [Theory] 
        [InlineData("4532015112830366")] // 16-cifret Visa (Luhn gyldigt) 
        [InlineData("4532-0151-1283-0366")] // Tilladte bindestreger 
        [InlineData("4532 0151 1283 0366")] // Tilladte mellemrum 
        [InlineData("378282246310005")] // 15-cifret Amex (Luhn gyldigt) 
        public void Constructor_WithValidCreditCards_CreatesInstanceSuccessfully(string validCardNumber) 
        { 
            var exception = Record.Exception(() => new CreditCardNumber(validCardNumber)); 
            Assert.Null(exception); 
        }
        // =========================================================================
        // KATEGORI 3: Invalid Input Testing (Fejlende Luhn-checksum og forbudte tegn)
        // =========================================================================

        [Theory]
        [InlineData("4532015112830367")] // Forkert kontrolciffer (Fejler Luhn Mod 10)
        [InlineData("1234567812345671")] // RETTET: Sum = 61 (Fejler garanteret Luhn!)
        [InlineData("453201511283A366")] // Indeholder bogstaver
        [InlineData("453201511283'366")] // Indeholder SQL injection-tegn
        public void Constructor_WithInvalidLuhnOrCharacters_ThrowsArgumentException(string invalidCard)
        {
            Assert.Throws<ArgumentException>(() => new CreditCardNumber(invalidCard));
        }

        [Theory]
        [InlineData("123456789")]                // For kort (< 13 tegn)
        [InlineData("123456789012345678901234")] // For langt (> 19/23 tegn)
        public void Constructor_WithOutOfRangeLength_ThrowsArgumentException(string outOfRangeCard)
        {
            // ThrowsAny accepterer både ArgumentException og den nedarvede ArgumentOutOfRangeException
            Assert.ThrowsAny<ArgumentException>(() => new CreditCardNumber(outOfRangeCard));
        }

        // =========================================================================
        // KATEGORI 4: PCI-DSS Sikkerhedsverifikation (Maskering af data)
        // =========================================================================

        [Fact]
        public void ToString_AlwaysReturnsMaskedValue_NeverExposingFullPAN()
        {
            // Arrange
            string rawCard = "4532015112830366";
            var card = new CreditCardNumber(rawCard);

            // Act
            string displayedString = card.ToString();

            // Assert: Kun de sidste 4 cifre må være synlige
            Assert.Equal("************0366", displayedString);
            Assert.DoesNotContain("45320151", displayedString);
        }
    }
}
