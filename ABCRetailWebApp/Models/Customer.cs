using System.ComponentModel.DataAnnotations;

namespace ABCRetailWebApp.Models
{
    public class Customer
    {
        public string id { get; set; } = Guid.NewGuid().ToString();  // Unique identifier for each customer

        [Required(ErrorMessage = "Username is required")]
        public string Username { get; set; }  // Customer username for login

        [Required(ErrorMessage = "Password is required")]
        [DataType(DataType.Password)]
        public string Password { get; set; }  // Customer password for login

        [Required(ErrorMessage = "Name is required")]
        public string Name { get; set; }  // Customer name

        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email address")]
        public string Email { get; set; }  // Customer email

        [Required(ErrorMessage = "Delivery address is required")]
        public string DeliveryAddress { get; set; }  // Delivery address
    }
}
