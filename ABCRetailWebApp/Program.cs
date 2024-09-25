// File: Program.cs
using ABCRetailWebApp.Services;
using Azure.Storage.Blobs;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Http.Features;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Bind CosmosDbSettings section to CosmosDbSettings class
builder.Services.Configure<CosmosDbSettings>(builder.Configuration.GetSection("CosmosDbSettings"));

// Register CosmosClient as a Singleton
builder.Services.AddSingleton<CosmosClient>(sp =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    var connectionString = configuration.GetConnectionString("CosmosDB");
    return new CosmosClient(connectionString);
});

// Register IAdminsContainer using a Factory Method
builder.Services.AddSingleton<IAdminsContainer>(sp =>
{
    var cosmosClient = sp.GetRequiredService<CosmosClient>();
    var cosmosDbSettings = sp.GetRequiredService<IOptions<CosmosDbSettings>>().Value;
    return new AdminsContainer(
        cosmosClient,
        cosmosDbSettings.DatabaseName,
        cosmosDbSettings.Containers.Admins
    );
});

// Register IProductsContainer using a Factory Method
builder.Services.AddSingleton<IProductsContainer>(sp =>
{
    var cosmosClient = sp.GetRequiredService<CosmosClient>();
    var cosmosDbSettings = sp.GetRequiredService<IOptions<CosmosDbSettings>>().Value;
    return new ProductsContainer(
        cosmosClient,
        cosmosDbSettings.DatabaseName,
        cosmosDbSettings.Containers.Products
    );
});

// Register IOrdersContainer using a Factory Method
builder.Services.AddSingleton<IOrdersContainer>(sp =>
{
    var cosmosClient = sp.GetRequiredService<CosmosClient>();
    var cosmosDbSettings = sp.GetRequiredService<IOptions<CosmosDbSettings>>().Value;
    return new OrdersContainer(
        cosmosClient,
        cosmosDbSettings.DatabaseName,
        cosmosDbSettings.Containers.Orders
    );
});

// Register ICustomersContainer using a Factory Method
builder.Services.AddSingleton<ICustomersContainer>(sp =>
{
    var cosmosClient = sp.GetRequiredService<CosmosClient>();
    var cosmosDbSettings = sp.GetRequiredService<IOptions<CosmosDbSettings>>().Value;
    return new CustomersContainer(
        cosmosClient,
        cosmosDbSettings.DatabaseName,
        cosmosDbSettings.Containers.Customers
    );
});

// Register Azure Blob Service
builder.Services.AddSingleton(sp =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    var blobStorageConnectionString = configuration.GetConnectionString("BlobStorage");
    return new BlobServiceClient(blobStorageConnectionString); // Add BlobServiceClient as a singleton
});

// Configure File Upload Size Limit
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 52428800; // Set to 50MB (or adjust according to your needs)
});

// Configure Session with enhanced settings
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always; // Ensures cookies are only sent over HTTPS
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseSession();  // Enable session handling
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
