// File: Services/CosmosDbSettings.cs
namespace ABCRetailWebApp.Services
{
    public class CosmosDbSettings
    {
        public string DatabaseName { get; set; }
        public ContainersSettings Containers { get; set; }
    }

    public class ContainersSettings
    {
        public string Customers { get; set; }
        public string Orders { get; set; }
        public string Products { get; set; }
        public string Admins { get; set; }
    }
}
