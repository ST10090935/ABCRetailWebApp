using Microsoft.AspNetCore.Mvc;
using ABCRetailWebApp.Models;
using Microsoft.Azure.Cosmos;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace ABCRetailWebApp.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class OrderController : Controller
    {
        private readonly Container _orderContainer;
        private readonly Container _productContainer;
        private readonly Container _customerContainer;

        public OrderController(Container orderContainer, Container productContainer, Container customerContainer)
        {
            _orderContainer = orderContainer;
            _productContainer = productContainer;
            _customerContainer = customerContainer;
        }

        // GET: /Order/Create
        [HttpGet("create")]
        public async Task<IActionResult> Create(string customerId)
        {
            if (string.IsNullOrEmpty(customerId))
            {
                return RedirectToAction("Register", "Customer");
            }

            // Fetch customer information
            var customerResponse = await _customerContainer.ReadItemAsync<Customer>(customerId, new PartitionKey(customerId));
            var customer = customerResponse.Resource;

            // Fetch available products
            var productsQuery = "SELECT * FROM c";
            var productIterator = _productContainer.GetItemQueryIterator<Product>(productsQuery);
            var products = new List<Product>();

            while (productIterator.HasMoreResults)
            {
                var response = await productIterator.ReadNextAsync();
                products.AddRange(response);
            }

            ViewBag.Products = products;
            ViewBag.CustomerId = customerId;  // Pass CustomerId to the view
            ViewBag.CustomerName = customer.Name;  // Pass CustomerName to the view
            ViewBag.CustomerEmail = customer.Email;  // Pass CustomerEmail to the view
            ViewBag.DeliveryAddress = customer.DeliveryAddress;  // Pass DeliveryAddress to the view

            return View(new Order { CustomerId = customerId });
        }

        // POST: /Order/Create
        [HttpPost("create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateOrder([FromForm] Order order)
        {
            if (!ModelState.IsValid)
            {
                return View(order);
            }

            await _orderContainer.CreateItemAsync(order);
            TempData["Message"] = "Order created successfully!";
            return RedirectToAction("Index");
        }
    }
}
