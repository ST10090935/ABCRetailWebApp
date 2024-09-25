using Microsoft.AspNetCore.Mvc;
using ABCRetailWebApp.Models;
using ABCRetailWebApp.Services;
using Microsoft.Azure.Cosmos;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace ABCRetailWebApp.Controllers
{
    [Route("customer")]
    public class CustomerController : Controller
    {
        private readonly IProductsContainer _productContainer;
        private readonly IOrdersContainer _orderContainer;
        private readonly ICustomersContainer _customerContainer;
        private readonly ILogger<CustomerController> _logger;

        public CustomerController(
            IProductsContainer productContainer,
            IOrdersContainer orderContainer,
            ICustomersContainer customerContainer,
            ILogger<CustomerController> logger)
        {
            _productContainer = productContainer;
            _orderContainer = orderContainer;
            _customerContainer = customerContainer;
            _logger = logger;
        }

        // GET: /customer/register
        [HttpGet("register")]
        public IActionResult Register()
        {
            return View(new Customer());
        }

        // POST: /customer/register
        [HttpPost("register")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register([FromForm] Customer customer)
        {
            if (!ModelState.IsValid)
            {
                return View(customer);
            }

            try
            {
                // Create the customer in Cosmos DB
                await _customerContainer.Instance.CreateItemAsync(customer, new PartitionKey(customer.id));
                TempData["Message"] = "Customer registered successfully!";
                return RedirectToAction("Login"); // Redirect to login after registration
            }
            catch (CosmosException ex)
            {
                _logger.LogError(ex, "Error creating customer.");
                ViewBag.ErrorMessage = $"Error creating customer: {ex.Message}";
                return View("Error");
            }
        }

        // GET: /customer/login
        [HttpGet("login")]
        public IActionResult Login()
        {
            if (HttpContext.Session.GetString("CustomerLoggedIn") == "true")
            {
                return RedirectToAction("Products");  // Redirect to Products if already logged in
            }

            return View(new Customer());
        }

        // POST: /customer/login
        [HttpPost("login")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login([FromForm] Customer customer)
        {
            _logger.LogInformation("POST /customer/login invoked.");

            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Login failed: Invalid ModelState");
                _logger.LogWarning($"Errors: {string.Join(",", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage))}");
                return View(customer);
            }

            // Use parameterized query to prevent SQL injection
            var query = "SELECT * FROM c WHERE c.Username = @username AND c.Password = @password";
            var queryDefinition = new QueryDefinition(query)
                .WithParameter("@username", customer.Username)
                .WithParameter("@password", customer.Password);

            var queryIterator = _customerContainer.Instance.GetItemQueryIterator<Customer>(queryDefinition);
            var customers = new List<Customer>();

            while (queryIterator.HasMoreResults)
            {
                var response = await queryIterator.ReadNextAsync();
                customers.AddRange(response);
            }

            if (customers.Count > 0)
            {
                // Set session values correctly
                HttpContext.Session.SetString("CustomerLoggedIn", "true");
                HttpContext.Session.SetString("CustomerId", customers[0].id);
                HttpContext.Session.SetString("CustomerName", customers[0].Name);

                _logger.LogInformation("User {Username} logged in successfully.", customer.Username);
                return RedirectToAction("Products");
            }

            ViewBag.ErrorMessage = "Invalid username or password.";
            _logger.LogWarning("Login failed for Username: {Username}", customer.Username);
            return View(customer);
        }

        // GET: /customer/products
        [HttpGet("products")]
        public async Task<IActionResult> Products()
        {
            if (HttpContext.Session.GetString("CustomerLoggedIn") != "true")
            {
                return RedirectToAction("Login");  // Redirect to login if not logged in
            }

            // Fetch available products
            var query = "SELECT * FROM c";
            var iterator = _productContainer.Instance.GetItemQueryIterator<Product>(query);
            var products = new List<Product>();

            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                products.AddRange(response);
            }

            return View(products);  // Pass products to the view
        }

        // GET: /customer/order/{productId}
        [HttpGet("order/{productId}")]
        public async Task<IActionResult> Order(string productId)
        {
            if (HttpContext.Session.GetString("CustomerLoggedIn") != "true")
            {
                return RedirectToAction("Login");  // Redirect to login if not logged in
            }

            try
            {
                // Fetch product details
                var productResponse = await _productContainer.Instance.ReadItemAsync<Product>(productId, new PartitionKey(productId));
                var product = productResponse.Resource;

                if (product == null)
                {
                    ViewBag.ErrorMessage = "Product not found.";
                    return View("Error");
                }

                var order = new Order
                {
                    CustomerId = HttpContext.Session.GetString("CustomerId"),
                    CustomerName = HttpContext.Session.GetString("CustomerName"),
                    Products = new List<Product> { product }  // Add the product to the order
                };

                return View(order);
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                ViewBag.ErrorMessage = "Product not found.";
                return View("Error");
            }
        }

        // POST: /customer/order
        [HttpPost("order")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateOrder([FromForm] Order order)
        {
            _logger.LogInformation("POST /customer/order invoked.");

            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Invalid order creation attempt for CustomerId: {CustomerId}", order.CustomerId);
                return View(order);
            }

            try
            {
                // Save the order to Azure Cosmos DB
                await _orderContainer.Instance.CreateItemAsync(order, new PartitionKey(order.CustomerId));
                TempData["Message"] = "Order created successfully!";
                return RedirectToAction("Products");
            }
            catch (CosmosException ex)
            {
                _logger.LogError(ex, "Error creating order.");
                ViewBag.ErrorMessage = $"Error creating order: {ex.Message}";
                return View("Error");
            }
        }

        // GET: /customer/logout
        [HttpGet("logout")]
        public IActionResult Logout()
        {
            HttpContext.Session.Clear(); // Clear all session data
            return RedirectToAction("Index", "Home");
        }
    }
}
