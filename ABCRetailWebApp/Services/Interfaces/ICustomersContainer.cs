using Microsoft.Azure.Cosmos;

namespace ABCRetailWebApp.Services
{
    public interface ICustomersContainer
    {
        Container Instance { get; }
    }
}
