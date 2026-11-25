

namespace WebGoatCore.Models
{
    /// <summary>
    /// Ny in-memory kurvmodel oprettet som arkitektonisk forbedring.
    /// Indkapsler varelinjer i kundens session og garanterer positiv total via Quantity.
    /// </summary>
    public class CartItem
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; }
        public decimal UnitPrice { get; set; }

        // Refaktoreret: Stærk typning garanterer, at Quantity altid er et positivt tal (1-240)
        public Quantity Quantity { get; set; }

        // Sikker beregning: Da Quantity altid er >= 1, kan TotalPrice ALDRIG blive negativ (medmindre at UnitPrice er negativ)!
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