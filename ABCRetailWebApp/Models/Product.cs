namespace ABCRetailWebApp.Models
{
    public class Product
    {
        public string id { get; set; } = Guid.NewGuid().ToString();  // Unique identifier for each product
        public string Name { get; set; }  // Product name
        public string Description { get; set; }  // Product description
        public decimal Price { get; set; }  // Product price
        public string? ImageUrl { get; set; }  // Optional image URL
    }
}
