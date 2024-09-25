using System.ComponentModel.DataAnnotations;

namespace ABCRetailWebApp.Models
{
    public class Admin
    {
        [Required]
        public string id { get; set; } = Guid.NewGuid().ToString();

        [Required]
        [StringLength(50, MinimumLength = 3)]
        public string Username { get; set; }

        [Required]
        [DataType(DataType.Password)]
        [StringLength(100, MinimumLength = 6)]
        public string Password { get; set; }

        // Additional properties can be added here
    }
}
