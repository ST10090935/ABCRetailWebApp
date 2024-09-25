using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;

namespace ABCRetailWebApp.Services
{
    public interface IProductsContainer
    {
        Container Instance { get; }

        Task<ItemResponse<T>> CreateItemAsync<T>(T item);
        Task<ItemResponse<T>> ReadItemAsync<T>(string id, PartitionKey partitionKey);
        Task<ItemResponse<T>> ReplaceItemAsync<T>(T item, string id, PartitionKey partitionKey);
        Task<ItemResponse<T>> DeleteItemAsync<T>(string id, PartitionKey partitionKey); // Added method
    }
}
