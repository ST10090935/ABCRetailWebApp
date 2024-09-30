using Microsoft.AspNetCore.Mvc;
using ABCRetailWebApp.Models;
using ABCRetailWebApp.Services;
using Microsoft.Azure.Cosmos;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Linq;

namespace ABCRetailWebApp.Controllers
{
    public class AdminController : Controller
    {
        private readonly IAdminsContainer _adminContainer;
        private readonly IProductsContainer _productContainer;
        private readonly ILogger<AdminController> _logger;

        public AdminController(IAdminsContainer adminContainer, IProductsContainer productContainer, ILogger<AdminController> logger)
        {
            _adminContainer = adminContainer;
            _productContainer = productContainer;
            _logger = logger;
        }

        [HttpGet("admin/register")]
        public IActionResult Register()
        {
            return View(new Admin());
        }

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
                admin.Password = BCrypt.Net.BCrypt.HashPassword(admin.Password);
                await _adminContainer.Instance.CreateItemAsync(admin, new PartitionKey(admin.id));

                TempData["Message"] = "Admin registered successfully!";
                return RedirectToAction("Login", "Admin");
            }
            catch (CosmosException ex)
            {
                _logger.LogError(ex, "Error creating admin with ID: {AdminId}", admin.id);
                ViewBag.ErrorMessage = "An unexpected error occurred while registering. Please try again.";
                return View("Error");
            }
        }

        [HttpGet("admin/login")]
        public IActionResult Login()
        {
            if (HttpContext.Session.GetString("AdminLoggedIn") == "true")
            {
                return RedirectToAction("ManageProducts");
            }

            return View(new Admin());
        }

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

                    bool isPasswordValid = BCrypt.Net.BCrypt.Verify(admin.Password, storedHashedPassword);

                    if (isPasswordValid)
                    {
                        HttpContext.Session.SetString("AdminLoggedIn", "true");
                        return RedirectToAction("ManageProducts");
                    }
                }

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

        [HttpGet("admin/manage-products")]
        public async Task<IActionResult> ManageProducts()
        {
            if (HttpContext.Session.GetString("AdminLoggedIn") != "true")
            {
                return RedirectToAction("Login", "Admin");
            }

            try
            {
                var query = "SELECT * FROM c";
                var iterator = _productContainer.Instance.GetItemQueryIterator<Product>(query);
                var products = new List<Product>();

                while (iterator.HasMoreResults)
                {
                    var response = await iterator.ReadNextAsync();
                    products.AddRange(response);
                }

                return View(products);
            }
            catch (CosmosException ex)
            {
                _logger.LogError(ex, "Error fetching products.");
                ViewBag.ErrorMessage = "An unexpected error occurred while fetching products.";
                return View("Error");
            }
        }

        [HttpGet("admin/create-product")]
        public IActionResult CreateProduct()
        {
            if (HttpContext.Session.GetString("AdminLoggedIn") != "true")
            {
                return RedirectToAction("Login", "Admin");
            }

            return View();
        }

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

        [HttpGet("admin/edit-product/{id}")]
        public async Task<IActionResult> EditProduct(string id)
        {
            if (HttpContext.Session.GetString("AdminLoggedIn") != "true")
            {
                return RedirectToAction("Login", "Admin");
            }

            try
            {
                var response = await _productContainer.Instance.ReadItemAsync<Product>(id, new PartitionKey(id));
                return View(response.Resource);
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("Product with ID: {ProductId} not found.", id);
                return NotFound();
            }
            catch (CosmosException ex)
            {
                _logger.LogError(ex, "Error fetching product with ID: {ProductId}", id);
                ViewBag.ErrorMessage = "An unexpected error occurred while fetching the product.";
                return View("Error");
            }
        }

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
                product.id = id; // Ensure the product ID is set correctly
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

        [HttpPost("admin/delete-product")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteProduct(string id)
        {
            if (HttpContext.Session.GetString("AdminLoggedIn") != "true")
            {
                return RedirectToAction("Login", "Admin");
            }

            try
            {
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

        [HttpGet("admin/logout")]
        public IActionResult Logout()
        {
            HttpContext.Session.SetString("AdminLoggedIn", "false");
            return RedirectToAction("Index", "Home");
        }
    }
}
