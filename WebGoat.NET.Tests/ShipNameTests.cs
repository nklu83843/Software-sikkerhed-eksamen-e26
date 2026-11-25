using System;
using WebGoatCore.Models;
using Xunit;

namespace WebGoat.NET.Tests.Domain
{
    public class ShipNameTests
    {
        // =========================================================================
        // KATEGORI 1 & 2: Normal & Boundary Testing
        // =========================================================================

        [Theory]
        [InlineData("Bo")]                                  // Nedre grænse: præcis 2 tegn
        [InlineData("Alexander Møller")]                   // Danske tegn (Æ, Ø, Å)
        [InlineData("Hans-Christian Andersen")]             // Tilladt bindestreg
        [InlineData("DetteErEtMegetLangtNavnPaaFyrreBogstaver")] // Øvre grænse: 40 tegn
        public void Constructor_WithValidNames_CreatesInstance(string validName)
        {
            var exception = Record.Exception(() => new ShipName(validName));
            Assert.Null(exception);
        }

        // =========================================================================
        // KATEGORI 3: Invalid Input Testing (SQL Injection & Specialtegn)
        // =========================================================================

        [Theory]
        [InlineData("A")]                                // For kort (< 2 tegn)
        [InlineData("NavnDerErAltForLangtOgOverskriderMaksimumGraensenPaaFyrreTegnMedFlereBogstaver")] // For langt (> 40 tegn)
        [InlineData("Alexander123")]                    // Indeholder tal
        [InlineData("John Doe'")]                       // SQL Injection pling-tegn fra Øvelse 33
        [InlineData("' OR '1'='1")]                     // Klassisk SQL Auth bypass
        [InlineData("Test; DROP TABLE Orders;--")]       // Destruktiv SQL Injection vektor
        [InlineData("<script>alert('XSS')</script>")]   // Cross-Site Scripting vektor
        public void Constructor_WithInvalidInput_ThrowsArgumentException(string invalidInput)
        {
            Assert.Throws<ArgumentException>(() => new ShipName(invalidInput));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Constructor_WithNullOrWhitespace_ThrowsArgumentNullException(string emptyInput)
        {
            Assert.Throws<ArgumentNullException>(() => new ShipName(emptyInput));
        }

        // =========================================================================
        // KATEGORI 4: Extreme Input Testing (ReDoS og Buffer Overløb)
        // =========================================================================

        [Fact]
        public void Constructor_WithMassiveString_ThrowsArgumentExceptionFastWithoutReDoS()
        {
            var massiveString = new string('A', 50000);
            var watch = System.Diagnostics.Stopwatch.StartNew();
            Assert.Throws<ArgumentException>(() => new ShipName(massiveString));
            watch.Stop();

            Assert.True(watch.ElapsedMilliseconds < 50, "Valideringen tog for lang tid; risiko for ReDoS!");
        }
    }
}
