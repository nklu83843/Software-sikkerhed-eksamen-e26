using WebGoatCore.Models;
using WebGoatCore.Data;
using Microsoft.AspNetCore.Mvc;
using System;


namespace WebGoatCore.Controllers
{
    [Route("[controller]/[action]")]
    public class CartController : Controller
    {
        private readonly ProductRepository _productRepository;

        public CartController(ProductRepository productRepository)
        {
            _productRepository = productRepository;
        }

        private Cart GetCart()
        {
            HttpContext.Session.TryGet<Cart>("Cart", out var cart);
            if(cart == null)
            {
                cart = new Cart();
            }
            return cart;
        }

        public IActionResult Index()
        {
            return View(GetCart());
        }
/*  VULNERABLE to AC-08 Anti-Hamlet
        [HttpPost("{productId}")]
        public IActionResult AddOrder(int productId, short quantity)
        {
            if(quantity <= 0)
            {
                return RedirectToAction("Details", "Product", new { productId = productId, quantity = quantity });
            }

            var product = _productRepository.GetProductById(productId);
            
            var cart = GetCart();
            if(!cart.OrderDetails.ContainsKey(productId))
            {
                var orderDetail = new OrderDetail()
                {
                    Discount = 0.0F,
                    ProductId = productId,
                    Quantity = quantity,
                    Product = product,
                    UnitPrice = product.UnitPrice
                };
                cart.OrderDetails.Add(orderDetail.ProductId, orderDetail);
            }
            else
            {
                var originalOrder = cart.OrderDetails[productId];
                originalOrder.Quantity += quantity;
            }

            HttpContext.Session.Set("Cart", cart);

            return RedirectToAction("Index");
        }
*/

/*
    AC-08: Sikkerhedsudbedring af Anti-hamlet med:

    I overensstemmelse med principperne i Secure by Design (Kapitel 5) erstattes den primitive int med et Domæneprimitiv (Domain Primitive). Et domæneprimitiv er et uforanderligt (immutable) værdiobjekt, der opfylder følgende ufravigelige krav:

    1. Værdiobjekt (Value Object): Objektet har ingen særskilt identitet; to instanser med samme numeriske værdi betragtes som identiske.
    2. Uforanderlighed (Immutability): Værdien kan ikke ændres efter instansiering (read-only property uden public setter).
    3. Validity by Existence: Objektet garanterer sin egen gyldighed ved sin rene eksistens. Det kan kun oprettes, hvis samtlige forretningsregler og invarianter er opfyldt.
    4. Fail-Fast ved Konstruktion: Hvis input er ugyldigt, kaster konstruktøren omgående en exception.

    se
        WebGoat.NET/Models/Quantity.cs
        WebGoat.NET/Models/CartItem.cs

*/      // Ved at fjerne "{productId}" fra rute-skabelonen sikres en ren og ensartet POST-formularhåndtering, 
        // hvor ID og mængde følges ad i samme datanyttelast over netværket frem for at være splittet mellem URL-sti og form-felter.
        [HttpPost] 
        // Attributten "[ValidateAntiForgeryToken]" sikrer, at serveren afviser anmodningen, medmindre den indeholder det hemmelige, 
        // kryptografiske token, som webshoppens legitime Razor-formular har genereret via "@Html.AntiForgeryToken()"
        // Modvirker Cross-Site Request Forgery (CSRF / CWE-352) sårbarheden
        [ValidateAntiForgeryToken] 
        // Løsningen med string og Quantity.TryParse: Ved at modtage dataene som en rå tekststreng ("string quantityInput") anerkender vi, at data, 
        // der krydser Tillidsgrænse 1 (TB1) fra en ubetroet browser, er Tainted Data
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

        [HttpGet("{productId}")]
        public IActionResult RemoveOrder(int productId)
        {
            try
            {
                var cart = GetCart();
                if (!cart.OrderDetails.ContainsKey(productId))
                {
                    return View("RemoveOrderError", string.Format("Product {0} was not found in your cart.", productId));
                }

                cart.OrderDetails.Remove(productId);
                HttpContext.Session.Set("Cart", cart);

                Response.Redirect("~/ViewCart.aspx");
            }
            catch (Exception ex)
            {
                return View("RemoveOrderError", string.Format("An error has occurred: {0}", ex.Message));
            }

            return RedirectToAction("Index");
        }
    }
}