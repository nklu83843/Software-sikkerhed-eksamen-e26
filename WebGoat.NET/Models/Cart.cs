using System.Collections.Generic;
using System.Linq;


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
        public void AddOrUpdateItem(int productId, string productName, double unitPrice, Quantity quantity)
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
