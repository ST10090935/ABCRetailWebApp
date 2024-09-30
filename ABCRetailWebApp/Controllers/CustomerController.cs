// File: Controllers/CustomerController.cs
using Microsoft.AspNetCore.Mvc;
using ABCRetailWebApp.Models;
using ABCRetailWebApp.Services;
using Microsoft.Azure.Cosmos;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using System.Linq;
using Microsoft.AspNetCore.Http;
using System;

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
                _logger.LogWarning("Registration failed: Invalid ModelState");
                return View(customer);
            }

            try
            {
                // Check if username already exists
                var usernameQuery = "SELECT * FROM c WHERE c.Username = @username";
                var queryDefinition = new QueryDefinition(usernameQuery)
                    .WithParameter("@username", customer.Username);

                var queryIterator = _customerContainer.Instance.GetItemQueryIterator<Customer>(queryDefinition);
                var existingCustomers = new List<Customer>();

                while (queryIterator.HasMoreResults)
                {
                    var response = await queryIterator.ReadNextAsync();
                    existingCustomers.AddRange(response);
                }

                if (existingCustomers.Any())
                {
                    ModelState.AddModelError("Username", "Username is already taken.");
                    _logger.LogWarning("Registration failed: Username '{Username}' is already taken.", customer.Username);
                    return View(customer);
                }

                // Assign a new ID to the customer if it's not set (already handled by model's default)
                if (string.IsNullOrEmpty(customer.id))
                {
                    customer.id = Guid.NewGuid().ToString();
                }

                // Hash the password before storing
                customer.Password = BCrypt.Net.BCrypt.HashPassword(customer.Password);

                // Create the customer in Cosmos DB
                await _customerContainer.Instance.CreateItemAsync(customer, new PartitionKey(customer.id));

                TempData["Message"] = "Customer registered successfully!";
                _logger.LogInformation("Customer '{Username}' registered successfully.", customer.Username);
                return RedirectToAction("Login");
            }
            catch (CosmosException ex)
            {
                _logger.LogError(ex, "Cosmos DB error while creating customer.");
                ViewBag.ErrorMessage = $"Error creating customer: {ex.Message}";
                return View("Error");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during customer registration.");
                ViewBag.ErrorMessage = "An unexpected error occurred. Please try again.";
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
                _logger.LogWarning($"Errors: {string.Join(", ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage))}");
                return View(customer);
            }

            try
            {
                // Use parameterized query to prevent SQL injection
                var query = "SELECT * FROM c WHERE c.Username = @username";
                var queryDefinition = new QueryDefinition(query)
                    .WithParameter("@username", customer.Username);

                var queryIterator = _customerContainer.Instance.GetItemQueryIterator<Customer>(queryDefinition);
                var customers = new List<Customer>();

                while (queryIterator.HasMoreResults)
                {
                    var response = await queryIterator.ReadNextAsync();
                    customers.AddRange(response);
                }

                if (customers.Count > 0)
                {
                    var storedHashedPassword = customers.First().Password;
                    bool isPasswordValid = BCrypt.Net.BCrypt.Verify(customer.Password, storedHashedPassword);

                    if (isPasswordValid)
                    {
                        // Set session values correctly
                        HttpContext.Session.SetString("CustomerLoggedIn", "true");
                        HttpContext.Session.SetString("CustomerId", customers.First().id);
                        HttpContext.Session.SetString("CustomerName", customers.First().Name);

                        _logger.LogInformation("User '{Username}' logged in successfully.", customer.Username);
                        return RedirectToAction("Products");
                    }
                }

                ViewBag.ErrorMessage = "Invalid username or password.";
                _logger.LogWarning("Login failed for Username: {Username}", customer.Username);
                return View(customer);
            }
            catch (CosmosException ex)
            {
                _logger.LogError(ex, "Cosmos DB error during customer login.");
                ViewBag.ErrorMessage = "An unexpected error occurred while logging in. Please try again.";
                return View("Error");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during customer login.");
                ViewBag.ErrorMessage = "An unexpected error occurred. Please try again.";
                return View("Error");
            }
        }

        // GET: /customer/products
        [HttpGet("products")]
        public async Task<IActionResult> Products()
        {
            if (HttpContext.Session.GetString("CustomerLoggedIn") != "true")
            {
                return RedirectToAction("Login");  // Redirect to login if not logged in
            }

            try
            {
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
            catch (CosmosException ex)
            {
                _logger.LogError(ex, "Cosmos DB error while fetching products.");
                ViewBag.ErrorMessage = "An unexpected error occurred while fetching products.";
                return View("Error");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while fetching products.");
                ViewBag.ErrorMessage = "An unexpected error occurred. Please try again.";
                return View("Error");
            }
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
            catch (CosmosException ex)
            {
                _logger.LogError(ex, "Cosmos DB error while fetching product with ID: {ProductId}", productId);
                ViewBag.ErrorMessage = "An unexpected error occurred while fetching the product.";
                return View("Error");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during order creation.");
                ViewBag.ErrorMessage = "An unexpected error occurred. Please try again.";
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
                return View("Order", order); // Return to Order view with existing data
            }

            try
            {
                // Assign a new ID to the order if it's not set
                if (string.IsNullOrEmpty(order.id))
                {
                    order.id = Guid.NewGuid().ToString();
                }

                // Save the order to Azure Cosmos DB
                await _orderContainer.Instance.CreateItemAsync(order, new PartitionKey(order.CustomerId));

                TempData["Message"] = "Order created successfully!";
                _logger.LogInformation("Order '{OrderId}' created successfully for CustomerId: {CustomerId}", order.id, order.CustomerId);
                return RedirectToAction("Products");
            }
            catch (CosmosException ex)
            {
                _logger.LogError(ex, "Cosmos DB error while creating order.");
                ViewBag.ErrorMessage = $"Error creating order: {ex.Message}";
                return View("Error");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during order creation.");
                ViewBag.ErrorMessage = "An unexpected error occurred. Please try again.";
                return View("Error");
            }
        }

        // GET: /customer/logout
        [HttpGet("logout")]
        public IActionResult Logout()
        {
            HttpContext.Session.Clear(); // Clear all session data
            _logger.LogInformation("Customer logged out.");
            return RedirectToAction("Index", "Home");
        }
    }
}
