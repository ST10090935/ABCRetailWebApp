using System;
using System.Collections.Generic;

namespace ABCRetailWebApp.Models
{
    public class Order
    {
        public string id { get; set; } = Guid.NewGuid().ToString();  // Cosmos DB requires lowercase 'id'
        public string CustomerId { get; set; }  // Reference to the Customer who placed the order
        public string CustomerName { get; set; }  // Customer name for the order
        public string CustomerEmail { get; set; }  // Customer email for notifications
        public DateTime OrderDate { get; set; } = DateTime.UtcNow;  // Date of the order
        public string DeliveryAddress { get; set; }  // Delivery address for the order
        public List<string> ProductIds { get; set; } = new List<string>();  // List of product IDs associated with the order
        public List<Product> Products { get; set; } = new List<Product>();  // List of product details for reference
        public OrderStatus Status { get; set; } = OrderStatus.Pending;  // Status of the order
    }

    public enum OrderStatus
    {
        Pending,
        Shipped,
        Delivered,
        Cancelled
    }
}
