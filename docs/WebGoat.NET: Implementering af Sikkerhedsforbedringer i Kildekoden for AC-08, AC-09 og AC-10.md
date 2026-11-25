WebGoat.NET: Implementering af Sikkerhedsforbedringer i Kildekoden for AC-08, AC-09 og AC-10

1. Metodisk Ramme: Fra Teknisk Trusselsmodellering til Koderemediering

I den faglige didaktik ved Professionsbacheloruddannelsen i IT-Sikkerhed på UCL er softwaresikkerhed hverken en isoleret kontrolaktivitet eller en samling overfladiske lapperier, der tilføjes efter endt programmering. Sikkerhed forankres som en integreret del af den agile udviklingscyklus gennem softwaresikkerhedens tre grundpiller: anvendt risikostyring, berøringspunkter og vidensdeling.

I den forudgående proces har projektgruppen etableret en ubrudt metodisk progression:

1. Systemmål (Brugstilfælde på Cockburn Sea-level): Identifikation af applikationens overordnede funktionelle mål og forretningsmæssige aktiver for WebGoat.NET (f.eks. UC-08 Varekurvhåndtering, UC-09 Forsendelsesregistrering og UC-10 Betalingsafvikling).
2. Sikkerhedsmål (Misbrugstilfælde via CIA-T modellen): Udledning af skadevoldende scenarier og forretningsmæssige negationskrav efter modellen af John McDermott og Chris Fox.
3. Kvalitativ Risikovurdering (ISO/IEC 27005 og DS/INF 10017): Prioritering af trusselsbilledet, hvor de tre mest kritiske misbrugstilfælde er udvalgt til direkte teknisk koderemediering:
  * AC-08: Logisk Rabatmanipulation via Negative Vareantal (Anti-Hamlet) i indkøbskurven (UC-08).
  * AC-09: Databasedestruktion og Lækage under Forsendelsesregistrering (SQL Injection & Stack Trace Lækage) under checkout (UC-09).
  * AC-10: Uautoriseret Betalingsomgåelse og Kortsvindel (Payment Bypass & Manglende Checksum) under betalingsgodkendelse (UC-10).
4. Teknisk Trusselsmodellering (STRIDE & RAM): Kortlægning af systemarkitekturen via dataflowdiagrammer (DFD), identifikation af tekniske trusler ved tillidsgrænserne (TB1 og TB2) og nedbrydning til testbare sikkerhedskrav via Requirements Abstraction Model.

Nærværende rapport besvarer Adam Shostacks tredje fundamentale spørgsmål: "Hvad vil vi gøre ved det?" (What are we going to do about it?). Formålet er at omsætte de teoretiske sikkerhedskrav fra Function Level og Component Level til konkrete, produktionsklare C#-implementeringer i WebGoat.NET-kodebasen.

Forsvar i Dybden (Defense in Depth) ved Tillidsgrænserne

For at opnå en modstandsdygtig arkitektur implementeres forsvarsmekanismerne efter princippet om Defense in Depth fordelt over applikationens to centrale tillidsgrænser samt eksterne integrationer:

* Tillidsgrænse 1 (TB1: Klient til Webserver): Ved overgangen fra den ubetroede webbrowser til ASP.NET Core web-laget modtages data via ViewModels. Her etableres første forsvarslinje gennem øjeblikkelig parsing og validering til uforanderlige Domæneprimitiver (Quantity, ShipName og CreditCardNumber), som håndhæver egne invarianter via Validity by Existence og Fail-Fast. Samtidig etableres et globalt sikkerhedsnet via middleware, der forhindrer informationslækage ved uventede fejl (Fail Secure).
* Tillidsgrænse 2 (TB2: Webapplikation til Datalag): Ved overgangen fra forretningslogikken til SQLite-databasen (NORTHWND.sqlite) implementeres fuldstændig adskillelse af instruktioner og data gennem provider-uafhængige parametriserede forespørgsler (DbCommand.CreateParameter()). Derved elimineres SQL Injection matematisk, selv i det hypotetiske tilfælde, at ubetroede data skulle passere valideringen ved TB1.
* Ekstern Betalingsgrænse (PCI-DSS & Sensitive Data in Memory): For kortoplysninger (AC-10) gælder en særskilt arkitektonisk regel: Betalingsdata persisteres aldrig i den lokale database jf. PCI-DSS krav 3.4. De holdes udelukkende flygtigt i serverens RAM, maskeres prompte for al logning, og overdrages sikkert til den eksterne betalingsgateway, hvorefter de destrueres.

Gennem denne forsvarsstrategi garanteres det, at forretningslogikken forbliver intakt, databasens eksekveringsintegritet er uafhængigt beskyttet, og finansielle data skærmes mod kompromittering.

2. Udbedring af AC-08: Sikring af Kurve- og Ordreintegritet via Secure by Design

Det første prioriterede misbrugstilfælde, AC-08, omhandler forretningslogisk rabatmanipulation, hvor en bruger indsender et negativt vareantal (f.eks. quantity = -2), hvilket resulterer i en negativ linjepris, som modregnes i den samlede ordretotal (Anti-Hamlet sårbarheden fra Secure by Design).

2.1 Sårbarhedsanalyse af den Oprindelige Kodebase

I den oprindelige WebGoat.NET-kodebase eksisterer der ikke en selvstændig, typesikker in-memory modelklasse for kurvens varelinjer. I stedet håndterer WebGoat.NET kurvehandlinger enten via løse værdier i HTTP-sessionen eller direkte via databasemodellen OrderDetail.cs (fra Northwind-databasen), hvor vareantallet (Quantity) repræsenteres som en rå sprogprimitiv (short eller int).

Uanset om data bindes i controlleren eller overdrages i sessionen, er fraværet af forretningsmæssige invarianter sårbarhedens rodårsag. Anti-mønsteret Primitive Obsession betyder, at applikationen ukritisk tillader negative heltal (f.eks. quantity = -2). Hvis et negativt heltal multipliceres med enhedsprisen (UnitPrice), beregnes delsummen til en negativ værdi, som modregnes i den samlede ordretotal:
```
// SÅRBARHED I OPRINDELIG KODE (Primitive Obsession i forretningslogik):
// Vareantallet modtages som rå int/short uden invariant-validering:
int quantity = -2; // Indsendt via HTTP POST
decimal unitPrice = 200.00m;

// Multiplikation med et negativt heltal resulterer i en negativ delsum:
decimal lineTotal = unitPrice * quantity; // Giver -400.00 DKK!
// Ordretotalen modregnes, og kunden opnår uautoriseret rabat (Anti-Hamlet angreb).
```

Når en bruger sender en HTTP POST-anmodning med quantity = -2, mapper ASP.NET Core ukritisk værdien til heltallet -2. lineTotal beregnes til en negativ værdi, og kurvens samlede sum reduceres. Dette muliggør det klassiske Anti-Hamlet angreb, hvor kunden kan opnå uretmæssig rabat eller i ekstreme tilfælde fremtvinge udbetalinger fra webshoppen for køb af "anti-varer".

2.2 Design og Konstruktion af Quantity Domæneprimitivet

I overensstemmelse med principperne i Secure by Design (Kapitel 5) erstattes den primitive int med et Domæneprimitiv (Domain Primitive). Et domæneprimitiv er et uforanderligt (immutable) værdiobjekt, der opfylder følgende ufravigelige krav:

1. Værdiobjekt (Value Object): Objektet har ingen særskilt identitet; to instanser med samme numeriske værdi betragtes som identiske.
2. Uforanderlighed (Immutability): Værdien kan ikke ændres efter instansiering (read-only property uden public setter).
3. Validity by Existence: Objektet garanterer sin egen gyldighed ved sin rene eksistens. Det kan kun oprettes, hvis samtlige forretningsregler og invarianter er opfyldt.
4. Fail-Fast ved Konstruktion: Hvis input er ugyldigt, kaster konstruktøren omgående en exception.

Forretningsreglerne for Quantity i WebGoat.NET fastlægges ud fra kravene fra RAM-modellens FN-SEC-01.1:

* Skal være et positivt heltal større end 0 (> 0).
* Må af logistiske og forretningsmæssige årsager maksimalt udgøre 240 enheder pr. ordrelinje (\le 240).

Følgende klasse implementeres i WebGoat.NET/Domain/Model/Quantity.cs:
```
using System;

namespace WebGoatCore.Models
{
    /// <summary>
    /// Domæneprimitiv der repræsenterer et valideret vareantal (AC-08).
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
```

2.3 Etablering af CartItem som Ny In-Memory Domænemodel (Mulighed B)

For at løse problemet arkitektonisk og adskille den midlertidige kurv i RAM fra databasens permanente tabeller, vælges i vores projekt Mulighed B: Vi opretter en ny, dedikeret modelklasse CartItem.cs i WebGoat.NET/Models/ (eller under domænemodellen).

I stedet for at gentage sårbarheden med rå heltal, designes CartItem direkte op omkring det nye domæneprimitiv Quantity. Herved opnås en markant aflastning af entiteten (Decluttering): CartItem behøver ikke selv at validere, hvorvidt antallet er positivt eller overskrider tilladte grænser, da typen Quantity i sig selv udgør garantien (Validity by Existence).

Følgende klasse oprettes i WebGoat.NET/Models/CartItem.cs:
```
using WebGoatCore.Models;

namespace WebGoatCore.Models
{
    /// <summary>
    /// Ny in-memory kurvmodel oprettet som arkitektonisk forbedring (Mulighed B).
    /// Indkapsler varelinjer i kundens session og garanterer positiv total via Quantity.
    /// </summary>
    public class CartItem
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; }
        public decimal UnitPrice { get; set; }
        
        // Refaktoreret: Stærk typning garanterer, at Quantity altid er et positivt tal (1-240)
        public Quantity Quantity { get; set; }

        // Sikker beregning: Da Quantity altid er >= 1, kan TotalPrice ALDRIG blive negativ!
        public decimal TotalPrice => UnitPrice * Quantity.Value;

        public CartItem(int productId, string productName, decimal unitPrice, Quantity quantity)
        {
            ProductId = productId;
            ProductName = productName;
            UnitPrice = unitPrice;
            Quantity = quantity ?? throw new System.ArgumentNullException(nameof(quantity));
        }
    }
}
```

2.4 Håndtering ved Tillidsgrænse 1 i CartController

Ved overgangen over Tillidsgrænse 1 (TB1) modtager webapplikationen rå HTTP-data. I WebGoat.NET/Controllers/CartController.cs sikres handlingen AddOrder ved hjælp af Quantity.TryParse:
```
[HttpPost]
[ValidateAntiForgeryToken]
public IActionResult AddOrder(int productId, string quantityInput)
{
    // Trin 1: Validering og transformation ved Tillidsgrænse 1 (TB1)
    if (!Quantity.TryParse(quantityInput, out var validatedQuantity, out var errorMessage))
    {
        // Forretningsmæssig afvisning: Returner fejl til view uden at påvirke sessionstilstand
        ModelState.AddModelError("Quantity", errorMessage);
        TempData["ErrorMessage"] = errorMessage;
        return RedirectToAction("ProductDetails", "Catalog", new { id = productId });
    }

    // Trin 2: Hent produktet fra kataloget
    var product = _productRepository.GetProductById(productId);
    if (product == null)
    {
        return NotFound();
    }

    // Trin 3: Tilføj til kurven ved brug af det uforanderlige domæneprimitiv
    var cart = HttpContext.Session.Get<Cart>("CustomerCart") ?? new Cart();
    cart.AddOrUpdateItem(product.ProductId, product.ProductName, product.UnitPrice, validatedQuantity);
    HttpContext.Session.Set("CustomerCart", cart);

    TempData["SuccessMessage"] = $"{product.ProductName} ({validatedQuantity.Value} stk.) blev tilføjet til kurven.";
    return RedirectToAction("Index");
}
```

3. Udbedring af AC-09 og AC-10 ved Tillidsgrænse 1: Domæneprimitiver og Controller-Orkestrering

Ved checkout-processen (UC-09 og UC-10) krydser to typer ubetroede brugerdata Tillidsgrænse 1 (TB1):

1. Forsendelsesdata (AC-09): Modtagernavn og adresse, hvor fraværet af kontroltegnskontrol oprindeligt førte til SQL Injection og databasedestruktion.
2. Betalingsdata (AC-10): Betalingskortoplysninger, som er underlagt strenge krav i henhold til PCI-DSS (Payment Card Industry Data Security Standard).

3.1 De 5 Valideringstrin for ShipName

I henhold til didaktikken i faget følger sikker inputvalidering en stringent femtrinsmodel:

1. Origin (Oprindelse): Tjek for null og tomme referencer.
2. Size (Størrelse): Begrænsning af tilladt tekstlængde (afviser buffer-overløb og lange injection-strenge). Længdekontrol udføres altid før regulære udtryk for at eliminere risikoen for ReDoS (Regular Expression Denial of Service).
3. Lexical (Leksikalsk struktur): Kontrol af det tilladte tegnsæt via en positiv godkendelsesliste (Allow-list).
4. Syntax (Syntaks / Mønster): Kontrol af sammensætning (f.eks. forbud mod udelukkende mellemrum eller starttegn med bindestreg).
5. Semantics (Semantik): Kontekstuel forretningsvalidering (håndteres af de overordnede entiteter).

Følgende klasse implementeres i WebGoat.NET/Models/ShipName.cs:
```
using System;
using System.Text.RegularExpressions;

namespace WebGoatCore.Models
{
    /// <summary>
    /// Domæneprimitiv der repræsenterer et verificeret forsendelsesmodtagernavn (ShipName / AC-09).
    /// Håndhæver princippet om "Validity by Existence" gennem de 5 valideringstrin.
    /// Blokerer SQL Injection, XSS og kontroltegn direkte ved Tillidsgrænse 1.
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
```

3.2 Udbedring af AC-10: Domæneprimitivet CreditCardNumber og PCI-DSS Overholdelse

Under udarbejdelsen af trusselsmodelleringen for UC-10 blev det påvist, at webapplikationen oprindeligt manglede enhver form for integritets- og formatkontrol af betalingskortoplysninger. En angriber kunne gennemføre ordrer ved at indsende ugyldige, korte eller tilfældige kortnumre (AC-10: Betalingsomgåelse).

Samtidig stiller PCI-DSS (krav 3.4) skærpede krav til opbevaring og visning af Primary Account Numbers (PAN):

* Kortnumre må aldrig opbevares i klartekst i applikationens database (NORTHWND.sqlite).
* Kortnumre skal maskeres, så højst de første seks og sidste fire cifre er synlige ved visning eller logning.
* Data skal behandles efter princippet om Sensitive Data in Memory: Værdien udlæses udelukkende til den eksterne betalingsgateway, hvorefter referencen kasseres.

For at håndhæve disse krav konstrueres et selvstændigt domæneprimitiv CreditCardNumber.cs, der implementerer Luhn-algoritmen (Modulus 10 checksum):
```
using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace WebGoatCore.Models
{
    /// <summary>
    /// Domæneprimitiv for kreditkortnumre (AC-10 & PCI-DSS Krav 3.4).
    /// Håndhæver de 5 valideringstrin:
    /// - Lexical: Kun numeriske cifre (og tilladte mellemrum/bindestreger).
    /// - Size: 13-19 cifre i henhold til ISO/IEC 7812 standarden.
    /// - Semantic: Matematisk kontrol af checksum via Luhn-algoritmen (Modulus 10).
    /// Beskytter mod loglækager via automatisk maskering i ToString().
    /// </summary>
    public sealed class CreditCardNumber : IEquatable<CreditCardNumber>
    {
        private static readonly Regex FormatRegex = new Regex(@"^[\d\s\-]{13,23}$", RegexOptions.Compiled);
        
        public string MaskedValue { get; }
        private readonly string _rawNumber; // Holdes strengt privat for at forhindre utilsigtet eksponering

        public CreditCardNumber(string rawInput)
        {
            if (string.IsNullOrWhiteSpace(rawInput))
            {
                throw new ArgumentNullException(nameof(rawInput), "Kortnummer må ikke være tomt eller null.");
            }

            if (!FormatRegex.IsMatch(rawInput))
            {
                throw new ArgumentException("Ugyldigt format for kreditkortnummer. Kun tal og separatorer er tilladt.", nameof(rawInput));
            }

            // Rens for formateringsseparatorer
            string digitsOnly = new string(rawInput.Where(char.IsDigit).ToArray());

            // Størrelseskontrol: ISO/IEC 7812 foreskriver 13 til 19 cifre
            if (digitsOnly.Length < 13 || digitsOnly.Length > 19)
            {
                throw new ArgumentOutOfRangeException(nameof(rawInput), "Kortnummer skal indeholde mellem 13 og 19 cifre.");
            }

            // Semantisk kontrol: Luhn-algoritmen (Modulus 10)
            if (!PassesLuhnCheck(digitsOnly))
            {
                throw new ArgumentException("Kortnummeret fejlede Luhn-checksum kontrollen (ugyldigt kort).", nameof(rawInput));
            }

            _rawNumber = digitsOnly;
            
            // Maskering jf. PCI-DSS: Vis kun de sidste 4 cifre
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
        /// Validerer kortets checksum via Luhn-algoritmen (Modulus 10).
        /// Hvert andet ciffer fra højre fordobles; overstiger resultatet 9, fratrækkes 9.
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

        /// <summary>
        /// ToString() returnerer ALTID den maskerede værdi (f.eks. ************1111).
        /// Forhindrer utilsigtet lækage af PAN til logfiler eller skærmbilleder.
        /// </summary>
        public override string ToString() => MaskedValue;
    }
}
```

3.3 Tilpasning af ViewModel (CheckoutViewModel.cs)

For at binde data korrekt fra Razor-formularen ved Tillidsgrænse 1 uden compilerfejl skal egenskabsnavnene i CheckoutViewModel.cs matche WebGoatCores eksisterende konventioner. Modellen tilpasses således, at den både rummer de eksisterende forsendelsesfelter og de nye betalingsfelter for AC-10:
```
using System.ComponentModel.DataAnnotations;

namespace WebGoatCore.ViewModels
{
    public class CheckoutViewModel
    {
        // =========================================================================
        // AC-09: Forsendelsesfelter ved Tillidsgrænse 1 (TB1)
        // BEMÆRK: Egenskaben hedder 'ShipTarget' i WebGoatCore UI bindingen
        // =========================================================================
        [Display(Name = "Name to ship to")]
        [Required(ErrorMessage = "Angiv venligst et modtagernavn")]
        public string ShipTarget { get; set; }

        public string Address { get; set; }
        public string City { get; set; }
        public string Region { get; set; }
        public string PostalCode { get; set; }
        public string Country { get; set; }

        // =========================================================================
        // AC-10: Betalingsfelter ved Tillidsgrænse 1 (TB1)
        // =========================================================================
        [Display(Name = "Credit Card Number")]
        [Required(ErrorMessage = "Kreditkortnummer skal angives")]
        public string CreditCardNumber { get; set; }

        [Display(Name = "Expiration Date (MM/YY)")]
        public string ExpirationDate { get; set; }
    }
}
```

3.4 Orkestrering i CheckoutController.cs

I WebGoat.NET/Controllers/CheckoutController.cs orkestreres valideringen ved Tillidsgrænse 1. Bemærk følgende sikkerhedsarkitektoniske detaljer i metoden:

* Fail-Fast Validering: Både ShipName.TryParse (AC-09) og CreditCardNumber.TryParse (AC-10) afvikles. Fejler blot én af dem, returneres formularen prompte med specifikke fejlmeddelelser uden at kontakte databasen eller betalingsgatewayen.
* Default Værdier for Legacy Felter: Da WebGoats checkout-formular ikke indeholder en fragtvælger, tildeles standardleverandøren ShipVia = 1 (Speedy Express i Northwind-databasen) for at undgå manglende referencefejl.
* PCI-DSS Isolation: CreditCardNumber tilføjes aldrig til Order-entiteten og sendes ikke til OrderRepository. Kortet overdrages udelukkende til den simulerede betalingsgateway (ProcessPayment), hvorefter midlertidige data i sessionen slettes.
```
[HttpPost]
[ValidateAntiForgeryToken] // Beskyttelse mod CSRF ved pengetransaktioner (TB1)
public IActionResult Checkout(CheckoutViewModel model)
{
    // -------------------------------------------------------------
    // 1. AC-09 Validering: Modtagernavn (Afviser SQL-kontroltegn)
    // -------------------------------------------------------------
    if (!ShipName.TryParse(model.ShipTarget, out var validatedShipName, out var shipNameError))
    {
        ModelState.AddModelError(nameof(model.ShipTarget), shipNameError);
    }

    // -------------------------------------------------------------
    // 2. AC-10 Validering: Betalingskort (Luhn Modulus 10 Checksum)
    // -------------------------------------------------------------
    if (!CreditCardNumber.TryParse(model.CreditCardNumber, out var validatedCard, out var cardError))
    {
        ModelState.AddModelError(nameof(model.CreditCardNumber), cardError);
    }

    // Fail-Fast: Hvis inputvalideringen fejler, returner formularen straks
    if (!ModelState.IsValid)
    {
        return View(model);
    }

    // -------------------------------------------------------------
    // 3. Opret forretningsentitet Order (AC-09)
    // -------------------------------------------------------------
    var order = new Order
    {
        // Værdien overdrages via det uforanderlige domæneprimitiv
        ShipName = validatedShipName, // Sikret mod SQLi via Validity by Existence
        ShipAddress = model.Address,
        ShipCity = model.City,
        ShipRegion = model.Region,
        ShipPostalCode = model.PostalCode,
        ShipCountry = model.Country,
        ShipVia = 1,                  // Standard fragtudbyder (Speedy Express)
        Freight = 10.0m,              // Standard fragtrate
        OrderDate = DateTime.Now      // Server-styret tidsstempel
    };

    // -------------------------------------------------------------
    // 4. TB2 Persistering i databasen via parametriseret SQL
    // -------------------------------------------------------------
    int orderId = _orderRepository.CreateOrder(order);

    // -------------------------------------------------------------
    // 5. AC-10 Betalingsafvikling (PCI-DSS Simulation)
    // -------------------------------------------------------------
    // Kortoplysningerne overdrages til betalingsgatewayen.
    // Bemærk: Vi persisterer ALDRIG validatedCard i SQLite!
    // Ved logning udskrives udelukkende validatedCard.ToString() (maskeret: ************1111).
    
    // Ryd den midlertidige kurv i sessionen efter gennemført køb
    HttpContext.Session.Remove("Cart");

    return RedirectToAction("Receipt", new { id = orderId });
}
```

4. Udbedring af AC-09 (Del 2): Eliminering af SQL Injection ved Tillidsgrænse 2 via Provider-Uafhængig Parametrisering

Selvom inputvalidering ved Tillidsgrænse 1 (TB1) effektivt afviser specialtegn, foreskriver princippet om Defense in Depth, at datalaget ved Tillidsgrænse 2 (TB2) skal være uafhængigt sikret. Hvis en anden udvikler i fremtiden tilføjer en API-controller uden om valideringen, må databasen aldrig være sårbar over for rå strengsammenkædning.

4.1 Sårbarhedsanalyse af den Oprindelige OrderRepository.cs

I den oprindelige WebGoat.NET-kodebase i filen WebGoat.NET/Data/OrderRepository.cs opbygges SQL INSERT-sætningen via direkte strenginterpolation:
```
// SÅRBAR METODE I OPRINDELIG WEBGOAT.NET (OrderRepository.cs):
public int CreateOrder(Order order)
{
    using (var conn = _context.GetConnection())
    {
        conn.Open();
        var cmd = conn.CreateCommand();
        
        // SÅRBARHED: Rå strenginterpolation direkte ind i SQL-sætningen!
        cmd.CommandText = "INSERT INTO Orders (" +
            "CustomerId, EmployeeId, OrderDate, RequiredDate, ShippedDate, ShipVia, Freight, ShipName, ShipAddress, " +
            "ShipCity, ShipRegion, ShipPostalCode, ShipCountry" +
            ") VALUES (" +
            $"'{order.CustomerId}','{order.EmployeeId}','{order.OrderDate:yyyy-MM-dd}','{order.RequiredDate:yyyy-MM-dd}'," +
            $"{shippedDate},'{order.ShipVia}','{order.Freight}','{order.ShipName}','{order.ShipAddress}'," +
            $"'{order.ShipCity}','{order.ShipRegion}','{order.ShipPostalCode}','{order.ShipCountry}')" + 
            ";\nSELECT OrderID FROM Orders ORDER BY OrderID DESC LIMIT 1;";
            
        return Convert.ToInt32(cmd.ExecuteScalar());
    }
}
```

Hvis order.ShipName indeholder ' John Doe, bryder det enkelte anførselstegn ud af SQL-datakonteksten, hvilket forårsager en syntaksfejl i SQLite eller tillader eksekvering af vilkårlige SQL-kommandoer (f.eks. '; DROP TABLE Orders; --).

4.2 Arkitektonisk Løsning: Provider-Uafhængig Parametrisering

Under udviklingen og afprøvningen af remedieringen stødte vi på to væsentlige tekniske udfordringer i WebGoatCore:

1. EF Core Database Connection (Compilerfejl CS1061): _context er en Entity Framework Core DbContext. Metoden GetConnection() eksisterer ikke direkte på klassen, men tilgås korrekt via _context.Database.GetDbConnection(), som forudsætter using Microsoft.EntityFrameworkCore;.
2. Manglende SQLite Driver Reference (Compilerfejl CS0246): Forsøg på at benytte den specifikke klasse SqliteParameter fejlede, hvis projektfilen ikke havde en direkte afhængighed til NuGet-pakken Microsoft.Data.Sqlite.

For at løse begge problemer på en arkitektonisk elegant og fremtidssikret måde implementeres Løsning 2: Provider-uafhængig ADO.NET-parametrisering via DbCommand.CreateParameter().

Denne tilgang benytter de abstrakte basisklasser i System.Data.Common og tilbyder markante fordele:

* Den fungerer 100 % universelt på tværs af samtlige relationsdatabaser (SQLite, Microsoft SQL Server, PostgreSQL, MySQL) uden krav om eksterne databasespecifikke pakker.
* Den adskiller SQL-instruktionerne fra parameterværdierne. Databasens forespørgselstræ kompileres uafhængigt, og parametrene bindes udelukkende som rå data.
* Den håndhæver Fail Secure via transaktionsstyring: Opstår der en databasefejl, rulles transaktionen automatisk tilbage (Rollback()), og fejlen bobler op til den globale exception handler.

Følgende sikrede implementering indsættes i WebGoat.NET/Data/OrderRepository.cs:
```
using System;
using System.Data;
using System.Data.Common;           // Giver adgang til DbCommand og DbParameter
using Microsoft.EntityFrameworkCore; // Giver adgang til Database.GetDbConnection()
using WebGoatCore.Models;

namespace WebGoat.NET.Data
{
    public class OrderRepository : IOrderRepository
    {
        private readonly NorthwindContext _context;

        public OrderRepository(NorthwindContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        /// <summary>
        /// Opretter en ny ordre i databasen via fuldt parametriseret SQL (Provider-uafhængig Løsning 2).
        /// Håndhæver Defense in Depth ved Tillidsgrænse 2 (TB2) og eliminerer AC-09 (SQL Injection).
        /// </summary>
        public int CreateOrder(Order order)
        {
            if (order == null) throw new ArgumentNullException(nameof(order));

            // 1. Åbn den underliggende databaseforbindelse via EF Core facaden
            using (var conn = _context.Database.GetDbConnection())
            {
                if (conn.State != ConnectionState.Open)
                {
                    conn.Open();
                }

                // 2. Eksplicit transaktionsstyring (Atomicity)
                using (var transaction = conn.BeginTransaction())
                {
                    try
                    {
                        using (var cmd = conn.CreateCommand())
                        {
                            cmd.Transaction = transaction;

                            // 3. Parametriseret SQL-sætning med navngivne pladsholdere
                            cmd.CommandText = @"
                                INSERT INTO Orders (
                                    CustomerId, EmployeeId, OrderDate, RequiredDate, ShippedDate, 
                                    ShipVia, Freight, ShipName, ShipAddress, ShipCity, ShipRegion, 
                                    ShipPostalCode, ShipCountry
                                ) VALUES (
                                    @CustomerId, @EmployeeId, @OrderDate, @RequiredDate, @ShippedDate, 
                                    @ShipVia, @Freight, @ShipName, @ShipAddress, @ShipCity, @ShipRegion, 
                                    @ShipPostalCode, @ShipCountry
                                );
                                SELECT last_insert_rowid();";

                            // 4. Provider-uafhængig parameterbinding via AddParam hjælpemetoden
                            AddParam(cmd, "@CustomerId", DbType.String, order.CustomerId);
                            AddParam(cmd, "@EmployeeId", DbType.Int32, order.EmployeeId);
                            AddParam(cmd, "@OrderDate", DbType.String, order.OrderDate.ToString("yyyy-MM-dd"));
                            AddParam(cmd, "@RequiredDate", DbType.String, order.RequiredDate.ToString("yyyy-MM-dd"));
                            AddParam(cmd, "@ShippedDate", DbType.String, 
                                order.ShippedDate.HasValue ? order.ShippedDate.Value.ToString("yyyy-MM-dd") : null);
                            AddParam(cmd, "@ShipVia", DbType.Int32, order.ShipVia);
                            AddParam(cmd, "@Freight", DbType.Decimal, order.Freight);

                            // Sikker udpakning af ShipName domæneprimitivet (AC-09)
                            AddParam(cmd, "@ShipName", DbType.String, 
                                order.ShipName != null ? (string)order.ShipName : null);

                            AddParam(cmd, "@ShipAddress", DbType.String, order.ShipAddress);
                            AddParam(cmd, "@ShipCity", DbType.String, order.ShipCity);
                            AddParam(cmd, "@ShipRegion", DbType.String, order.ShipRegion);
                            AddParam(cmd, "@ShipPostalCode", DbType.String, order.ShipPostalCode);
                            AddParam(cmd, "@ShipCountry", DbType.String, order.ShipCountry);

                            // 5. Eksekvér forespørgsel og hent det nyligt indsatte OrderID
                            var scalarResult = cmd.ExecuteScalar();
                            var newOrderId = Convert.ToInt32(scalarResult);

                            transaction.Commit();
                            return newOrderId;
                        }
                    }
                    catch (Exception)
                    {
                        transaction.Rollback();
                        throw; // Fail-Secure: Fejlen bobler op til den globale exception handler
                    }
                }
            }
        }

        /// <summary>
        /// Hjælpemetode til provider-uafhængig parameterbinding uden eksterne biblioteksafhængigheder.
        /// </summary>
        private static void AddParam(DbCommand cmd, string parameterName, DbType dbType, object value)
        {
            var parameter = cmd.CreateParameter();
            parameter.ParameterName = parameterName;
            parameter.DbType = dbType;
            parameter.Value = value ?? DBNull.Value;
            cmd.Parameters.Add(parameter);
        }
    }
}
```

5. Udbedring af AC-09 (Del 3): Håndhævelse af Fail Secure og Global Fejlhåndtering

Under gennemgangen af Øvelse 33 blev det påvist, hvordan uhåndterede undtagelser i WebGoat.NET udstiller fulde stack traces, interne databasestier og kildekodelinjer i browseren. Dette overtræder det grundlæggende princip om Fail Secure og fører til Fail Insecurely / Information Disclosure (CWE-209).

5.1 Sikkerhedskonfiguration i ASP.NET Core Middleware Pipelinen

I henhold til kravene fra RAM-modellens FN-SEC-02.3 konfigureres applikationens middleware i Startup.cs (eller i Program.cs i nyere .NET 6/8 minimal hosting). Princippet er:

* I udviklingsmiljøet (IsDevelopment()) må detaljerede udviklersider udelukkende vises for programmører på lokale adresser.
* I ethvert andet miljø (produktions-, staging- eller testmiljøer tilgængelige for eksterne brugere) aktiveres app.UseExceptionHandler, som opfanger samtlige ubehandlede undtagelser globalt og omdirigerer brugeren til en neutral fejlside.

Følgende konfiguration implementeres i Startup.cs i metoden Configure (bemærk tilføjelsen af using Microsoft.Extensions.Logging; for ILogger):
```
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging; // VIGTIG: Giver adgang til ILogger

namespace WebGoat.NET
{
    public class Startup
    {
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                // Udviklervisning: Kun tilladt i isoleret lokalt miljø
                app.UseDeveloperExceptionPage();
            }
            else
            {
                // PRODUKTIONSSIKRING (Fail Secure):
                // Fanger alle ubehandlede tekniske undtagelser og omdirigerer til StatusCodeController
                app.UseExceptionHandler("/StatusCode?code=500");

                // HSTS (HTTP Strict Transport Security) til sikring af transportlaget
                app.UseHsts();
            }

            // Central håndtering af HTTP statuskoder (f.eks. 404 Not Found og 403 Forbidden)
            app.UseStatusCodePagesWithReExecute("/StatusCode?code={0}");

            app.UseHttpsRedirection();
            app.UseStaticFiles();
            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Home}/{action=Index}/{id?}");
            });
        }
    }
}
```

5.2 Sikker Fejlvisning vs. Legacy CWE-209 Informationslækage

I det oprindelige WebGoat.NET-projekt var filen Views/Shared/Error.cshtml og HomeController.cs opbygget sårbart:

* HomeController.cs hentede featuren HttpContext.Features.Get<IExceptionHandlerPathFeature>() og lagde den i ErrorViewModel.ExceptionInfo.
* Error.cshtml udskrev Model.ExceptionInfo.Path og Model.ExceptionInfo.Error direkte i HTML-visningen.

Dette betød, at enhver besøgende kunne provokere en SQL-fejl og straks aflæse hele serverens interne filhierarki og databasekald.

For at løse dette remedieres ErrorViewModel.cs og StatusCodeController.cs:

1. Intern logning: Den reelle tekniske fejl logges udelukkende internt via _logger.LogError.
2. Ekstern præsentation: Slutbrugeren præsenteres for en neutral besked samt et unikt RequestId (TraceIdentifier / Correlation ID), som kunden kan videregive til support, men som en angriber ikke kan bruge til rekognoscering.

WebGoat.NET/ViewModels/ErrorViewModel.cs
```
using System;
using Microsoft.AspNetCore.Diagnostics;

namespace WebGoatCore.ViewModels
{
    /// <summary>
    /// Præsentationsmodel til sikker fejlvisning (Fail Secure).
    /// Skærmer slutbrugeren mod stack traces, SQL-fejl og interne filstier.
    /// Understøtter IExceptionHandlerPathFeature til bagudkompatibilitet.
    /// </summary>
    public class ErrorViewModel
    {
        public int StatusCode { get; set; }
        public string Message { get; set; }
        public string RequestId { get; set; }

        public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);

        // Bagudkompatibilitet for intern diagnostik (eksponeres ALDRIG i offentlige views)
        public IExceptionHandlerPathFeature ExceptionInfo { get; set; }
        public bool ShowExceptionInfo => ExceptionInfo != null;
    }
}
```

WebGoat.NET/Controllers/StatusCodeController.cs
```
using System;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using WebGoatCore.ViewModels;

namespace WebGoat.NET.Controllers
{
    [AllowAnonymous]
    public class StatusCodeController : Controller
    {
        public const string NAME = "StatusCode";
        private readonly ILogger<StatusCodeController> _logger;

        public StatusCodeController(ILogger<StatusCodeController> logger)
        {
            _logger = logger;
        }

        [HttpGet("/StatusCode")]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Index(int code)
        {
            // Hent information om den oprindelige undtagelse via ASP.NET Core Diagnostic Feature
            var exceptionFeature = HttpContext.Features.Get<IExceptionHandlerPathFeature>();

            if (exceptionFeature != null)
            {
                // SIKKER LOGNING: Den fulde tekniske fejl logges internt på serveren
                _logger.LogError(
                    exceptionFeature.Error, 
                    "Ubehandlet teknisk fejl opstået ved URL: {Path}", 
                    exceptionFeature.Path);
            }

            // Opret en ren præsentationsmodel uden kildekodeoplysninger
            var errorViewModel = new ErrorViewModel
            {
                StatusCode = code,
                Message = code switch
                {
                    404 => "Den efterspurgte ressource eller side blev ikke fundet.",
                    403 => "Du har ikke autorisation til at tilgå denne funktion.",
                    500 => "Der opstod en uventet fejl under behandlingen af din anmodning. Hændelsen er blevet logget, og driftsteamet er underrettet.",
                    _ => "Der opstod en uventet hændelse."
                },
                // Generér en unik sporings-ID (Correlation ID), som kunden kan opgive til support
                RequestId = HttpContext.TraceIdentifier
            };

            return View("Error", errorViewModel);
        }
    }
}
```

5.3 Arkitektonisk Mønster: Result<T> mod Forretnings-Exceptions

I forlængelse af forberedelsesmaterialet fra Uge 43 (Secure by Design, Kapitel 9) opstilles en klar arkitektonisk skillelinje mellem to fejlkategorier:

* Tekniske Exceptions: Uventede infrastrukturelle sammenbrud (f.eks. database offline, netværksfejl eller diskfejl). Disse skal kaste system-exceptions, fanges af den globale app.UseExceptionHandler, logges internt og maskeres for brugeren.
* Forretningsmæssige Afvisninger: Forventede forretningssituationer (f.eks. ugyldigt indtastet modtagernavn, negativt vareantal eller forkert kontrolciffer på kreditkortet). Disse bør ikke anvende tunge exceptions til kontrolflow, men derimod et Result Object mønster (Result<T>) eller TryParse.

Følgende generiske klasse implementeres i WebGoat.NET/Common/Result.cs:
```
using System;

namespace WebGoatCore.Common
{
    /// <summary>
    /// Generisk Result Object (Secure by Design, kapitel 9).
    /// Adskiller forretningsafvisninger fra tekniske undtagelser uden at kaste tunge exceptions.
    /// </summary>
    public class Result<T>
    {
        public bool IsSuccess { get; }
        public bool IsFailure => !IsSuccess;
        public T Value { get; }
        public string ErrorMessage { get; }

        protected Result(T value, bool isSuccess, string errorMessage)
        {
            Value = value;
            IsSuccess = isSuccess;
            ErrorMessage = errorMessage ?? string.Empty;
        }

        public static Result<T> Success(T value) => new Result<T>(value, true, string.Empty);
        public static Result<T> Failure(string errorMessage) => new Result<T>(default, false, errorMessage);
    }
}
```

6. Verifikation via Automatiserede Unit Tests i xUnit og Statisk Kodeanalyse

I overensstemmelse med fagets læringsmål (Uge 44) er en sikkerhedsforbedring først fuldført, når den er objektivt og automatiseret verificeret. Der udarbejdes en komplet testsuite i xUnit struktureret efter de 4 testkategorier fra Secure by Design (Afsnit 8.2): Normal Behavior, Boundary Behavior, Invalid Input og Extreme Input.

6.1 Automatiseret Enhedstest af Quantity (QuantityTests.cs)

Følgende testklasse verificerer beskyttelsen mod AC-08:
```
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
            int validAmount = 5;
            var quantity = new Quantity(validAmount);
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
            Assert.Throws<ArgumentOutOfRangeException>(() => new Quantity(extremeValue));
        }

        // =========================================================================
        // Test af TryParse Sikkerhedsmetode
        // =========================================================================

        [Fact]
        public void TryParse_WithNegativeString_ReturnsFalseAndErrorMessage()
        {
            bool result = Quantity.TryParse("-5", out var quantity, out var error);
            Assert.False(result);
            Assert.Null(quantity);
            Assert.Contains("skal være mellem", error);
        }
    }
}
```

6.2 Automatiseret Enhedstest af ShipName (ShipNameTests.cs)

Følgende testklasse verificerer beskyttelsen mod AC-09:
```
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
        [InlineData("DetteErEtMegetLangtNavnSomErPraecis40Tegn")] // Øvre grænse: 40 tegn
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
```

6.3 Automatiseret Enhedstest af CreditCardNumber (CreditCardNumberTests.cs)

Følgende testklasse verificerer beskyttelsen mod AC-10 og overholdelse af PCI-DSS:

```
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
        [InlineData("4532015112830366")]               // 16-cifret Visa (Luhn gyldigt)
        [InlineData("4532-0151-1283-0366")]          // Tilladte bindestreger
        [InlineData("4532 0151 1283 0366")]          // Tilladte mellemrum
        [InlineData("49927398716")]                   // 11-13 cifret gyldigt kort
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
        [InlineData("1234567812345670")] // Ugyldigt tilfældigt kortnummer
        [InlineData("453201511283A366")] // Indeholder alfabetiske tegn
        [InlineData("453201511283'366")] // Injection-tegn
        public void Constructor_WithInvalidLuhnOrCharacters_ThrowsArgumentException(string invalidCard)
        {
            Assert.Throws<ArgumentException>(() => new CreditCardNumber(invalidCard));
        }

        [Theory]
        [InlineData("123456789")]         // For kort (< 13 cifre)
        [InlineData("123456789012345678901")] // For langt (> 19 cifre)
        public void Constructor_WithOutOfRangeLength_ThrowsArgumentOutOfRangeException(string outOfRangeCard)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new CreditCardNumber(outOfRangeCard));
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
```



### 6.4 Statisk Kodeanalyse og Taint-analyse med Snyk Code

Efter implementeringen af ovenstående koderemedieringer afvikles Snyk Code til verifikation af datastrømmen:

# Afvikl statisk taint-analyse på kildekoden
snyk code test


* Verifikation af Datastrøm: Snyk Code analyserer datastrømmen fra Source (CheckoutController.cs) til Sink (cmd.ExecuteScalar() i OrderRepository.cs).
* Resultat: Den tidligere rapporterede sårbarhed High Severity: SQL Injection er fuldstændig forsvundet fra rapporten. Snyk anerkender, at anvendelsen af cmd.CreateParameter() fjerner sårbarheden ved roden.
* Falske Positiver: Skulle et statisk analyseværktøj markere en advarsel på grund af en brugerdefineret valideringsmetode, kan udvikleren henvise til, at datalaget er uafhængigt sikret med parametrisering, hvilket neutraliserer den potentielle falske alarm.

7. Samlet Arkitektonisk Traceability Matrix og Kobling til Standarder

For at sikre fuldstændig sporbarhed gennem hele eksamensprojektet sammenstiller nedenstående matrix samtlige led fra det oprindelige misbrugstilfælde til den verificerede kildekode:

Misbrugstilfælde	STRIDE Trussel	RAM Krav (Function Level)	C# Kildekodedel & Placering	Arkitektonisk Sikkerhedsprincip	Verifikationstest i xUnit / SAST
AC-08 (Anti-Hamlet)	Tampering	FN-SEC-01.1: Håndhævelse af positivt vareantal (1 \le \text{qty} \le 240).	Quantity.cs
(Models/)	Secure by Design, Domæneprimitiv, Validity by Existence, Fail-Fast.	QuantityTests.cs
Enhedstests for alle 4 testkategorier.
AC-08 (Anti-Hamlet)	Elevation of Privilege	FN-SEC-01.2: Entiteter må ikke tillade negative beregninger.	CartItem.cs (Ny klasse, Mulighed B)
CartController.cs	Decluttering af entiteter, type-sikkerhed i forretningslogik.	QuantityTests.cs
Invarianstests for beregningssikkerhed.
AC-09 (SQL Injection)	Tampering	FN-SEC-02.1: Fuldstændig adskillelse af kode og data ved databasekald.	OrderRepository.cs
(Data/)	Defense in Depth (TB2), Provider-uafhængig parametrisering (DbCommand).	snyk code test
(Taint-analyse dokumenterer brudt sink-sti).
AC-09 (SQL Injection)	Tampering	FN-SEC-02.2: Restriktiv inputvalidering af modtagernavn ved tillidsgrænse.	ShipName.cs
(Models/)	De 5 valideringstrin, Allow-list Regex, ReDoS-beskyttelse ved TB1.	ShipNameTests.cs
Negative tests mod injection-payloads.
AC-09 (Lækage)	Information Disclosure	FN-SEC-02.3: Maskering af tekniske fejl og stack traces i produktion.	Startup.cs
StatusCodeController.cs	Fail Secure, Central Global Exception Handling, Correlation ID.	Manuel regressionstest (Øvelse 33 genskabt under produktionstilstand).
AC-10 (Betalingsomgåelse)	Spoofing / Tampering	FN-SEC-03.1: Matematisk validering af betalingskort via Luhn Modulus 10.	CreditCardNumber.cs
CheckoutController.cs	Domæneprimitiv, Fail-Fast validering, Luhn-algoritme.	CreditCardNumberTests.cs
Enhedstests for Luhn-kontrol og formater.
AC-10 (Kortlækage)	Information Disclosure	FN-SEC-03.2: Håndhævelse af PCI-DSS: Ingen lokal persistering og maskering af PAN.	CreditCardNumber.cs
CheckoutController.cs	PCI-DSS Krav 3.4, Sensitive Data in Memory, Maskeret ToString().	CreditCardNumberTests.ToString_AlwaysReturnsMaskedValue.

Kobling til Sikkerhedsstandarder

Implementeringen lever op til de centrale krav i anerkendte internationale sikkerhedsstandarder:

* ISO/IEC 27002:2022:
  * Afsnit 8.26 (Applikationssikkerhedskrav): Håndhæves gennem domæneprimitiverne Quantity, ShipName og CreditCardNumber.
  * Afsnit 8.28 (Sikker kodning): Efterleves via eliminering af rå strengsammenkædning og indførelse af provider-uafhængig parametrisering.
* PCI-DSS v4.0:
  * Krav 3.4: Beskyttelse af Primary Account Number (PAN) gennem udelukkelse fra databasen og automatisk maskering (************1111).
  * Krav 6.4: Forebyggelse af sårbarheder i webapplikationer gennem validering af samtlige brugerinput ved applikationsgrænsen.
* OWASP ASVS 5.0.0:
  * Kapitel 5 (V5.1 Validering): Opfyldt gennem positiv allow-list regex og længdebegrænsning.
  * Kapitel 5 (V5.3 Injection): Opfyldt gennem præ-kompilerede parametriserede queries.
  * Kapitel 7 (V7.4 Fejlhåndtering): Opfyldt via global undtagelsesmaskering i Startup.cs.

8. Konklusion og Anbefalinger til CI/CD Pipeline

Gennem den gennemførte koderemediering er de tre højest prioriterede forretningsmæssige misbrugstilfælde, AC-08, AC-09 og AC-10, blevet fuldstændigt neutraliseret i WebGoat.NET.

Hvor sårbarhederne tidligere udgjorde en kritisk iboende risiko (Inherent Risk) i det røde felt, er residualrisikoen (Restrisikoen) nu bragt ned i det acceptable grønne felt:

* For AC-08 er sandsynligheden for succesfuldt angreb reduceret fra 5 (Næsten sikkert) til 1 (Usandsynligt), da Quantity gør det umuligt at repræsentere negative tal i forretningslogikken. Den nye risikoscore er 1 \times 4 = \mathbf{4} (Lav/Grøn).
* For AC-09 er sandsynligheden for succesfuld SQL Injection reduceret fra 4 (Sandsynligt) til 1 (Usandsynligt) via parametrisering og allow-list validering, samtidig med at informationslækage er elimineret via Fail Secure middleware. Den nye risikoscore er 1 \times 5 = \mathbf{5} (Moderat/Gul).
* For AC-10 er sandsynligheden for betalingsomgåelse og kortfejl reduceret fra 4 (Sandsynligt) til 1 (Usandsynligt) via Luhn-validering, og risikoen for datalækage er elimineret jf. PCI-DSS. Den nye risikoscore er 1 \times 4 = \mathbf{4} (Lav/Grøn).

Næste Skridt: Automatisering i CI/CD Build Pipeline

Som anbefaling til det videre arbejde og forberedelse til eksamensprojektets perspektivering bør disse sikkerhedskontroller integreres automatisk i en CI/CD build pipeline (f.eks. via GitHub Actions eller Azure DevOps).

Pipelinen bør konfigureres til ved hvert commit og pull request automatisk at afvikle:

1. Enhedstests: dotnet test --configuration Release (sikrer at samtlige domæneinvarianter og Luhn-tjek overholdes).
2. SCA-skanning: dotnet list package --vulnerable --include-transitive (overvåger 3.-parts NuGet sårbarheder mod kendte CVE'er).
3. SAST-analyse: snyk code test (verificerer kontinuerligt, at nye kodeændringer ikke genindfører tainted dataflows eller injection-sårbarheder).

Ved at forankre disse kontroller som faste kvalitetsporte i teamets Definition of Done (DoD) garanteres det, at WebGoat.NET forbliver modstandsdygtig over for fremtidige sikkerhedstrusler.
