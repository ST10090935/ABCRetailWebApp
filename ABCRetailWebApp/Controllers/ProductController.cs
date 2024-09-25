using Microsoft.AspNetCore.Mvc;
using ABCRetailWebApp.Models;
using Microsoft.Azure.Cosmos;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;
using Microsoft.AspNetCore.Http;

namespace ABCRetailWebApp.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ProductController : Controller
    {
        private readonly Container _productContainer;
        private readonly BlobServiceClient _blobServiceClient;
        private const string ContainerName = "product-images";

        public ProductController(Container productContainer, BlobServiceClient blobServiceClient)
        {
            _productContainer = productContainer;
            _blobServiceClient = blobServiceClient;
        }

        // GET: /Product
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            if (HttpContext.Session.GetString("AdminLoggedIn") != "true")
            {
                return RedirectToAction("Login", "Admin");  // Redirect to admin login if not logged in
            }

            var query = "SELECT * FROM c";
            var iterator = _productContainer.GetItemQueryIterator<Product>(query);
            var results = new List<Product>();

            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                results.AddRange(response);
            }

            return View(results);
        }

        // GET: /Product/Create
        [HttpGet("create")]
        public IActionResult Create()
        {
            if (HttpContext.Session.GetString("AdminLoggedIn") != "true")
            {
                return RedirectToAction("Login", "Admin");  // Redirect to admin login if not logged in
            }

            return View(new Product());
        }

        // POST: /Product/Create
        [HttpPost("create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([FromForm] Product product, [FromForm] IFormFile? imageFile)
        {
            if (HttpContext.Session.GetString("AdminLoggedIn") != "true")
            {
                return RedirectToAction("Login", "Admin");  // Redirect to admin login if not logged in
            }

            if (!ModelState.IsValid)
            {
                return View(product);
            }

            // If an image file is provided, upload it to Azure Blob Storage
            if (imageFile != null && imageFile.Length > 0)
            {
                try
                {
                    var containerClient = _blobServiceClient.GetBlobContainerClient(ContainerName);
                    await containerClient.CreateIfNotExistsAsync(PublicAccessType.Blob);

                    // Generate a unique filename
                    var uniqueFileName = Guid.NewGuid().ToString() + "_" + imageFile.FileName;

                    var blobClient = containerClient.GetBlobClient(uniqueFileName);

                    using (var stream = imageFile.OpenReadStream())
                    {
                        await blobClient.UploadAsync(stream, new BlobHttpHeaders { ContentType = imageFile.ContentType });
                    }

                    // Set the ImageUrl to the blob URL
                    product.ImageUrl = blobClient.Uri.ToString();
                }
                catch (Exception ex)
                {
                    // Log and show error message if image upload fails
                    // (Assuming you have a logger configured)
                    ViewBag.ErrorMessage = "Image upload failed: " + ex.Message;
                    return View(product);
                }
            }

            try
            {
                // Add product to Cosmos DB (Ensure PartitionKey is properly handled)
                await _productContainer.CreateItemAsync(product, new PartitionKey(product.id));

                TempData["Message"] = "Product created successfully!";
                return RedirectToAction("Index");
            }
            catch (CosmosException ex)
            {
                ViewBag.ErrorMessage = "An error occurred while saving the product: " + ex.Message;
                return View(product);
            }
        }
    }
}
