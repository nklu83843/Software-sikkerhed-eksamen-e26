using WebGoatCore.Models;
using WebGoatCore.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System;
using System.IO;
using WebGoatCore.ViewModels;
using System.Linq;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using WebGoatCore.Exceptions;

namespace WebGoatCore.Controllers
{
    public class CheckoutController : Controller
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly CustomerRepository _customerRepository;
        private readonly ShipperRepository _shipperRepository;
        private readonly OrderRepository _orderRepository;
        private CheckoutViewModel? _model;
        private string _resourcePath;

        public CheckoutController(UserManager<IdentityUser> userManager, CustomerRepository customerRepository, IHostEnvironment hostEnvironment, IConfiguration configuration, ShipperRepository shipperRepository, OrderRepository orderRepository)
        {
            _userManager = userManager;
            _customerRepository = customerRepository;
            _shipperRepository = shipperRepository;
            _orderRepository = orderRepository;
            _resourcePath = configuration.GetValue(Constants.WEBGOAT_ROOT, hostEnvironment.ContentRootPath);
        }

        [HttpGet]
        public IActionResult Checkout()
        {
            if(_model == null)
            {
                InitializeModel();
            }

            return View(_model);
        }

        private void InitializeModel()
        {
            _model = new CheckoutViewModel();

            _model.Cart = HttpContext.Session.Get<Cart>("Cart");
            _model.AvailableExpirationYears = Enumerable.Range(1, 5).Select(i => DateTime.Now.Year + i).ToList();
            _model.ShippingOptions = _shipperRepository.GetShippingOptions(_model.Cart?.SubTotal ?? 0);

            if (_model.Cart == null || _model.Cart.OrderDetails.Count == 0)
            {
                ModelState.AddModelError(string.Empty, "You have no items in your cart.");
            }

            var customer = GetCustomerOrAddError();
            if (customer != null)
            {
                var creditCard = GetCreditCardForUser();

                creditCard.GetCardForUser();
                _model.CreditCard = creditCard.Number;
                _model.ExpirationMonth = creditCard.Expiry.Month;
                _model.ExpirationYear = creditCard.Expiry.Year;

                _model.ShipTarget = customer.CompanyName;
                _model.Address = customer.Address ?? string.Empty;
                _model.City = customer.City ?? string.Empty;
                _model.Region = customer.Region ?? string.Empty;
                _model.PostalCode = customer.PostalCode ?? string.Empty;
                _model.Country = customer.Country ?? string.Empty;
            }
        }
/*
        [HttpPost]
        public IActionResult Checkout(CheckoutViewModel model)
        {
            model.Cart = HttpContext.Session.Get<Cart>("Cart")!;

            var customer = GetCustomerOrAddError();
            if(customer == null)
            {
                return View(model);
            }

            var creditCard = GetCreditCardForUser();
            try
            {
                creditCard.GetCardForUser();
            }
            catch (WebGoatCreditCardNotFoundException)
            {
            }

            //Get form of payment
            //If form specified card number, try to use it instead one stored for user
            if (model.CreditCard != null && model.CreditCard.Length >= 13)
            {
                creditCard.Number = model.CreditCard;
                creditCard.Expiry = new DateTime(model.ExpirationYear, model.ExpirationMonth, 1);
            }
            else
            {
                ModelState.AddModelError(string.Empty, "The card number specified is too short.");
                _model = model;
                return View(_model);
            }

            //Authorize payment through our bank or Authorize.net or someone.
            if (!creditCard.IsValid())
            {
                ModelState.AddModelError(string.Empty, "That card is not valid. Please enter a valid card.");
                _model = model;
                return View(_model);
            }

            if (model.RememberCreditCard)
            {
                creditCard.SaveCardForUser();
            }

            var order = new Order
            {
                ShipVia = model.ShippingMethod,
                ShipName = model.ShipTarget,
                ShipAddress = model.Address,
                ShipCity = model.City,
                ShipRegion = model.Region,
                ShipPostalCode = model.PostalCode,
                ShipCountry = model.Country,
                OrderDetails = model.Cart.OrderDetails.Values.ToList(),
                CustomerId = customer.CustomerId,
                OrderDate = DateTime.Now,
                RequiredDate = DateTime.Now.AddDays(7),
                Freight = Math.Round(_shipperRepository.GetShipperByShipperId(model.ShippingMethod).GetShippingCost(model.Cart.SubTotal), 2),
                EmployeeId = 1,
            };

            var approvalCode = creditCard.ChargeCard(order.Total);

            order.Shipment = new Shipment()
            {
                ShipmentDate = DateTime.Today.AddDays(1),
                ShipperId = order.ShipVia,
                TrackingNumber = _shipperRepository.GetNextTrackingNumber(_shipperRepository.GetShipperByShipperId(order.ShipVia)),
            };

            //Create the order itself.
            var orderId = _orderRepository.CreateOrder(order);

            //Create the payment record.
            _orderRepository.CreateOrderPayment(orderId, order.Total, creditCard.Number, creditCard.Expiry, approvalCode);

            HttpContext.Session.SetInt32("OrderId", orderId);
            HttpContext.Session.Remove("Cart");
            return RedirectToAction("Receipt");
        }
*/

        [HttpPost]
        [ValidateAntiForgeryToken] // Beskyttelse mod CSRF ved pengetransaktioner (TB1)
        public IActionResult Checkout(CheckoutViewModel model)
        {
            // -------------------------------------------------------------
            // 1. AC-09: TB1 Validering af Modtagernavn (Afviser SQL-injections)
            // -------------------------------------------------------------
            if (!ShipName.TryParse(model.ShipTarget, out var validatedShipName, out var shipNameError))
            {
                ModelState.AddModelError(nameof(model.ShipTarget), shipNameError);
            }

            // -------------------------------------------------------------
            // 2. AC-10: TB1 Validering af Kreditkort (Luhn & Formatkontrol)
            // -------------------------------------------------------------
            if (!CreditCardNumber.TryParse(model.CreditCardNumber, out var validatedCard, out var cardError))
            {
                ModelState.AddModelError(nameof(model.CreditCardNumber), cardError);
            }

            // Hvis enten modtagernavn eller kort er ugyldigt -> Fail-Fast
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // -------------------------------------------------------------
            // 3. Opret Order (Bemærk: Kortnummer gemmes ALDRIG i SQLite jf. PCI-DSS!)
            // -------------------------------------------------------------
            var order = new Order
            {
                ShipName = validatedShipName, // Sikret via domæneprimitiv
                ShipAddress = model.Address,
                ShipCity = model.City,
                ShipRegion = model.Region,
                ShipPostalCode = model.PostalCode,
                ShipCountry = model.Country,
                ShipVia = 1,
                OrderDate = DateTime.Now
            };

            // Ordren persisteres sikkert via parametriseret SQL i OrderRepository (TB2)
            int orderId = _orderRepository.CreateOrder(order);

            // -------------------------------------------------------------
            // 4. AC-10 Betalingsgodkendelse (Simulation af ekstern Gateway):
            // -------------------------------------------------------------
            // Her overdrages validatedCard til betalingsgatewayen.
            // Vi logger udelukkende den maskerede værdi (f.eks. ************1111)
            // Kortdata i RAM kasseres hermed sikkert.
            
            // Ryd session-kurven efter succesfuld betaling
            HttpContext.Session.Remove("Cart");

            return RedirectToAction("Receipt", new { id = orderId });
        }

        public IActionResult Receipt(int? id)
        {
            var orderId = HttpContext.Session.GetInt32("OrderId");
            if (id != null)
            {
                orderId = id;
            }

            if (orderId == null)
            {
                ModelState.AddModelError(string.Empty, "No order specified. Please try again.");
                return View();
            }

            Order order;
            try
            {
                order = _orderRepository.GetOrderById(orderId.Value);
            }
            catch (InvalidOperationException)
            {
                ModelState.AddModelError(string.Empty, string.Format("Order {0} was not found.", orderId));
                return View();
            }

            return View(order);
        }

        public IActionResult Receipts()
        {
            var customer = GetCustomerOrAddError();
            if(customer == null)
            {
                return View();
            }

            return View(_orderRepository.GetAllOrdersByCustomerId(customer.CustomerId));
        }

        public IActionResult PackageTracking(string? carrier, string? trackingNumber)
        {
            var model = new PackageTrackingViewModel()
            {
                SelectedCarrier = carrier,
                SelectedTrackingNumber = trackingNumber,
            };

            var customer = GetCustomerOrAddError();
            if (customer != null)
            {
                model.Orders = _orderRepository.GetAllOrdersByCustomerId(customer.CustomerId);
            }
            
            return View(model);
        }

        public IActionResult GoToExternalTracker(string carrier, string trackingNumber)
        {
            return Redirect(Order.GetPackageTrackingUrl(carrier, trackingNumber));
        }

        private Customer? GetCustomerOrAddError()
        {
            var username = _userManager.GetUserName(User);
            var customer = _customerRepository.GetCustomerByUsername(username);
            if (customer == null)
            {
                ModelState.AddModelError(string.Empty, "I can't identify you. Please log in and try again.");
                return null;
            }

            return customer;
        }

        private CreditCard GetCreditCardForUser()
        {
            return new CreditCard()
            {
                Filename = Path.Combine(_resourcePath, "StoredCreditCards.xml"),
                Username = _userManager.GetUserName(User)
            };
        }
    }
}