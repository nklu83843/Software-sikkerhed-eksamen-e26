using System;
using System.ComponentModel.DataAnnotations;

#pragma warning disable CS8618 // Non-nullable field is uninitialized. Consider declaring as nullable.
namespace WebGoatCore.Models
{
    public class OrderDetail
    {
        public int OrderId { get; set; }
        public int ProductId { get; set; }
        public double UnitPrice { get; set; }
        // SÅRBARHED I OPRINDELIG KODE (Primitive Obsession i forretningslogik): 
        // 
        // Vareantallet modtages som rå int/short uden invariant-validering: int quantity = -2; 
        // 
        // Indsendt via HTTP POST decimal unitPrice = 200.00m;
        public short Quantity { get; set; }
        public float Discount { get; set; }

        public virtual Order Order { get; set; }
        public virtual Product Product { get; set; }

        // Multiplikation med et negativt heltal resulterer i en negativ delsum: 
        // 
        // decimal lineTotal = unitPrice * quantity; 
        // Giver -400.00 DKK! 
        // Ordretotalen modregnes, og kunden opnår uautoriseret rabat (Anti-Hamlet angreb).
        public decimal DecimalUnitPrice => Convert.ToDecimal(this.UnitPrice);
        public decimal ExtendedPrice => DecimalUnitPrice * Convert.ToDecimal(1 - Discount) * Quantity;
    }
}
