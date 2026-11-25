**Aha! Der har vi den præcise, faktiske implementering af `Cart` i WebGoatCore!** 

Nu giver det hele 100 % mening i forhold til den compilerfejl, du lige fik på linje 112 (`CS1061: 'Cart' does not contain a definition for 'AddOrUpdateItem'`).

Din `Cart`-klasse er bygget op omkring et dictionary:
```csharp
public IDictionary<int, OrderDetail> OrderDetails { get; set; }
```
hvor nøglen (`int`) er `ProductId`, og værdien er en **`OrderDetail`** (som indeholder varelinjen). Den har på nuværende tidspunkt **ingen metoder**, kun properties og en constructor.

Her er, hvordan du løser det på den absolut mest elegante og **Secure by Design** måde, så din `CartController.cs` virker fejlfrit:

---

### Løsningen: Tilføj `AddOrUpdateItem` direkte i din `Cart.cs`

Du skal blot tilføje metoden `AddOrUpdateItem` inde i din eksisterende `Cart.cs`. Den skal opdatere `OrderDetails`-dictionariet og gøre brug af det nye `Quantity`-domæneprimitiv.

Åbn din **`Cart.cs`** og opdater den til følgende:

```csharp
using System.Collections.Generic;
using System.Linq;
using WebGoat.NET.Domain.Model; // Giver adgang til Quantity domæneprimitivet (eller det namespace du har brugt)

namespace WebGoatCore.Models
{
    public class Cart
    {
        public IDictionary<int, OrderDetail> OrderDetails { get; set; }

        public decimal SubTotal =>
            OrderDetails.Values.Sum(od => od.ExtendedPrice);

        public Cart()
        {
            OrderDetails = new Dictionary<int, OrderDetail>();
        }

        /// <summary>
        /// Tilføjer en vare eller lægger til antallet ved brug af Quantity domæneprimitivet.
        /// Sikrer mod negative tal og overflow i indkøbskurven (Anti-Hamlet).
        /// </summary>
        public void AddOrUpdateItem(int productId, string productName, decimal unitPrice, Quantity quantity)
        {
            if (OrderDetails.ContainsKey(productId))
            {
                // Hvis varen allerede er i kurven: Læg det nye antal til det eksisterende via Quantity
                int currentQty = OrderDetails[productId].Quantity;
                int newTotalQty = currentQty + quantity.Value;

                // Nyt Quantity-objekt håndhæver at totalen ikke overskrider max (f.eks. 240)
                var updatedQuantity = new Quantity(newTotalQty);
                OrderDetails[productId].Quantity = (short)updatedQuantity.Value;
            }
            else
            {
                // Ny varelinje i kurven
                OrderDetails.Add(productId, new OrderDetail
                {
                    ProductId = productId,
                    UnitPrice = unitPrice,
                    Quantity = (short)quantity.Value,
                    Discount = 0
                });
            }
        }
    }
}
```

---

### Hvorfor dette er genialt til din eksamen:

1. **Compilerfejlen forsvinder:** Metoden `cart.AddOrUpdateItem(product.ProductId, product.ProductName, product.UnitPrice, validatedQuantity);` på linje 112 i `CartController.cs` matcher nu 1:1.
2. **Du respekterer WebGoats eksisterende arkitektur:** Du genbruger `IDictionary<int, OrderDetail>`, præcis som WebGoat oprindeligt er tænkt, men du indkapsler logikken sikkert inde i `Cart`.
3. **Anti-Hamlet elimineres ved roden:** Hverken tilføjelse af nye varer eller opsummering af eksisterende varer kan nogensinde få `Quantity` under 1 eller over 240, fordi `new Quantity(...)` overvåger invariansen!

Prøv at gemme `Cart.cs` med denne metode og kør `dotnet build` igen – så burde compileren være helt tilfreds!