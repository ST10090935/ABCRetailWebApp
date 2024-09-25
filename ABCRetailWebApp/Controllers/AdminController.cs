using Microsoft.AspNetCore.Mvc;
using ABCRetailWebApp.Models;
using ABCRetailWebApp.Services;
using Microsoft.Azure.Cosmos;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using BCrypt.Net; // Ensure BCrypt is installed via NuGet

namespace ABCRetailWebApp.Controllers
{
    public class AdminController : Controller
    {
        private readonly IAdminsContainer _adminContainer;
        private readonly IProductsContainer _productContainer;
        private readonly ILogger<AdminController> _logger;

        // Constructor with Dependency Injection for AdminsContainer, ProductsContainer, and ILogger
        public AdminController(IAdminsContainer adminContainer, IProductsContainer productContainer, ILogger<AdminController> logger)
        {
            _adminContainer = adminContainer;
            _productContainer = productContainer;
            _logger = logger;
        }

        /// <summary>
        /// Displays the Admin Registration page.
        /// </summary>
        [HttpGet("admin/register")]
        public IActionResult Register()
        {
            return View(new Admin());
        }

        /// <summary>
        /// Handles Admin Registration form submission.
        /// </summary>
        [HttpPost("admin/register")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register([FromForm] Admin admin)
        {
            if (!ModelState.IsValid)
            {
                return View(admin);
            }

            try
            {
                // Hash the admin's password using BCrypt before storing
                admin.Password = BCrypt.Net.BCrypt.HashPassword(admin.Password);

                // Create the admin in Cosmos DB with the PartitionKey (assuming 'Id' is the partition key)
                await _adminContainer.Instance.CreateItemAsync(admin, new PartitionKey(admin.id));

                TempData["Message"] = "Admin registered successfully!";
                return RedirectToAction("Login", "Admin"); // Redirect to Login after successful registration
            }
            catch (CosmosException ex)
            {
                _logger.LogError(ex, "Error creating admin with ID: {AdminId}", admin.id);
                ViewBag.ErrorMessage = "An unexpected error occurred while registering. Please try again.";
                return View("Error");
            }
        }

        /// <summary>
        /// Displays the Admin Login page.
        /// </summary>
        [HttpGet("admin/login")]
        public IActionResult Login()
        {
            // If already logged in, redirect to ManageProducts
            if (HttpContext.Session.GetString("AdminLoggedIn") == "true")
            {
                return RedirectToAction("ManageProducts");
            }

            return View(new Admin());
        }

        /// <summary>
        /// Handles Admin Login form submission.
        /// </summary>
        [HttpPost("admin/login")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login([FromForm] Admin admin)
        {
            if (!ModelState.IsValid)
            {
                return View(admin);
            }

            try
            {
                // Query Cosmos DB for the admin with the provided username
                var sqlQueryText = "SELECT * FROM c WHERE c.Username = @username";
                var queryDefinition = new QueryDefinition(sqlQueryText)
                    .WithParameter("@username", admin.Username);

                var queryIterator = _adminContainer.Instance.GetItemQueryIterator<Admin>(queryDefinition);
                var admins = new List<Admin>();

                while (queryIterator.HasMoreResults)
                {
                    var response = await queryIterator.ReadNextAsync();
                    admins.AddRange(response);
                }

                if (admins.Count > 0)
                {
                    var storedHashedPassword = admins.First().Password;

                    // Verify the provided password against the stored hashed password
                    bool isPasswordValid = BCrypt.Net.BCrypt.Verify(admin.Password, storedHashedPassword);

                    if (isPasswordValid)
                    {
                        // Set session variable to indicate admin is logged in
                        HttpContext.Session.SetString("AdminLoggedIn", "true");
                        return RedirectToAction("ManageProducts");
                    }
                }

                // If admin not found or password invalid
                ViewBag.ErrorMessage = "Invalid username or password.";
                return View(admin);
            }
            catch (CosmosException ex)
            {
                _logger.LogError(ex, "Error during admin login for username: {Username}", admin.Username);
                ViewBag.ErrorMessage = "An unexpected error occurred while logging in. Please try again.";
                return View("Error");
            }
        }

        /// <summary>
        /// Displays the Manage Products page with a list of all products.
        /// </summary>
        [HttpGet("admin/manage-products")]
        public async Task<IActionResult> ManageProducts()
        {
            // Check if admin is logged in
            if (HttpContext.Session.GetString("AdminLoggedIn") != "true")
            {
                return RedirectToAction("Login", "Admin");  // Redirect to Login if not authenticated
            }

            try
            {
                // Fetch all products from Cosmos DB
                var query = "SELECT * FROM c";
                var iterator = _productContainer.Instance.GetItemQueryIterator<Product>(query);
                var products = new List<Product>();

                while (iterator.HasMoreResults)
                {
                    var response = await iterator.ReadNextAsync();
                    products.AddRange(response);
                }

                return View(products);  // Pass the list of products to the view
            }
            catch (CosmosException ex)
            {
                _logger.LogError(ex, "Error fetching products.");
                ViewBag.ErrorMessage = "An unexpected error occurred while fetching products.";
                return View("Error");
            }
        }

        /// <summary>
        /// Displays the Create Product page.
        /// </summary>
        [HttpGet("admin/create-product")]
        public IActionResult CreateProduct()
        {
            // Check if admin is logged in
            if (HttpContext.Session.GetString("AdminLoggedIn") != "true")
            {
                return RedirectToAction("Login", "Admin");
            }

            return View();
        }

        /// <summary>
        /// Handles Create Product form submission.
        /// </summary>
        [HttpPost("admin/create-product")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateProduct([FromForm] Product product)
        {
            if (!ModelState.IsValid)
            {
                return View(product);
            }

            try
            {
                // Create the product in Cosmos DB with the PartitionKey (assuming 'Id' is the partition key)
                await _productContainer.Instance.CreateItemAsync(product, new PartitionKey(product.id));

                TempData["Message"] = "Product created successfully!";
                return RedirectToAction("ManageProducts");
            }
            catch (CosmosException ex)
            {
                _logger.LogError(ex, "Error creating product with ID: {ProductId}", product.id);
                ViewBag.ErrorMessage = "An unexpected error occurred while creating the product.";
                return View("Error");
            }
        }

        /// <summary>
        /// Displays the Edit Product page for a specific product.
        /// </summary>
        [HttpGet("admin/edit-product/{id}")]
        public async Task<IActionResult> EditProduct(string id)
        {
            // Check if admin is logged in
            if (HttpContext.Session.GetString("AdminLoggedIn") != "true")
            {
                return RedirectToAction("Login", "Admin");
            }

            try
            {
                // Read the specific product from Cosmos DB using the provided ID and PartitionKey
                var response = await _productContainer.Instance.ReadItemAsync<Product>(id, new PartitionKey(id));
                return View(response.Resource);
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("Product with ID: {ProductId} not found.", id);
                return NotFound(); // Return 404 if product not found
            }
            catch (CosmosException ex)
            {
                _logger.LogError(ex, "Error fetching product with ID: {ProductId}", id);
                ViewBag.ErrorMessage = "An unexpected error occurred while fetching the product.";
                return View("Error");
            }
        }

        /// <summary>
        /// Handles Edit Product form submission.
        /// </summary>
        [HttpPost("admin/edit-product/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditProduct(string id, [FromForm] Product product)
        {
            if (!ModelState.IsValid)
            {
                return View(product);
            }

            try
            {
                // Replace the existing product in Cosmos DB using the provided ID and PartitionKey
                await _productContainer.Instance.ReplaceItemAsync(product, id, new PartitionKey(id));

                TempData["Message"] = "Product updated successfully!";
                return RedirectToAction("ManageProducts");
            }
            catch (CosmosException ex)
            {
                _logger.LogError(ex, "Error updating product with ID: {ProductId}", id);
                ViewBag.ErrorMessage = "An unexpected error occurred while updating the product.";
                return View("Error");
            }
        }

        /// <summary>
        /// Displays the Delete Product confirmation page for a specific product.
        /// </summary>
        [HttpGet("admin/delete-product/{id}")]
        public async Task<IActionResult> DeleteProduct(string id)
        {
            // Check if admin is logged in
            if (HttpContext.Session.GetString("AdminLoggedIn") != "true")
            {
                return RedirectToAction("Login", "Admin");
            }

            try
            {
                // Read the specific product from Cosmos DB using the provided ID and PartitionKey
                var response = await _productContainer.Instance.ReadItemAsync<Product>(id, new PartitionKey(id));
                return View(response.Resource);
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("Product with ID: {ProductId} not found.", id);
                return NotFound(); // Return 404 if product not found
            }
            catch (CosmosException ex)
            {
                _logger.LogError(ex, "Error fetching product with ID: {ProductId}", id);
                ViewBag.ErrorMessage = "An unexpected error occurred while fetching the product.";
                return View("Error");
            }
        }

        /// <summary>
        /// Handles Delete Product form submission.
        /// </summary>
        [HttpPost("admin/delete-product/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteProductConfirmed(string id)
        {
            // Check if admin is logged in
            if (HttpContext.Session.GetString("AdminLoggedIn") != "true")
            {
                return RedirectToAction("Login", "Admin");
            }

            try
            {
                // Delete the specific product from Cosmos DB using the provided ID and PartitionKey
                await _productContainer.Instance.DeleteItemAsync<Product>(id, new PartitionKey(id));

                TempData["Message"] = "Product deleted successfully!";
                return RedirectToAction("ManageProducts");
            }
            catch (CosmosException ex)
            {
                _logger.LogError(ex, "Error deleting product with ID: {ProductId}", id);
                ViewBag.ErrorMessage = "An unexpected error occurred while deleting the product.";
                return View("Error");
            }
        }

        /// <summary>
        /// Logs out the admin by resetting the session variable.
        /// </summary>
        [HttpGet("admin/logout")]
        public IActionResult Logout()
        {
            HttpContext.Session.SetString("AdminLoggedIn", "false");
            return RedirectToAction("Index", "Home");
        }
    }
}