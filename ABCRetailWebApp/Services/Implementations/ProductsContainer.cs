﻿using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;

namespace ABCRetailWebApp.Services
{
    public class ProductsContainer : IProductsContainer
    {
        public Container Instance { get; }

        public ProductsContainer(CosmosClient cosmosClient, string databaseName, string containerName)
        {
            Instance = cosmosClient.GetContainer(databaseName, containerName);
        }

        public Task<ItemResponse<T>> CreateItemAsync<T>(T item)
        {
            return Instance.CreateItemAsync(item);
        }

        public Task<ItemResponse<T>> ReadItemAsync<T>(string id, PartitionKey partitionKey)
        {
            return Instance.ReadItemAsync<T>(id, partitionKey);
        }

        public Task<ItemResponse<T>> ReplaceItemAsync<T>(T item, string id, PartitionKey partitionKey)
        {
            return Instance.ReplaceItemAsync(item, id, partitionKey);
        }

        public Task<ItemResponse<T>> DeleteItemAsync<T>(string id, PartitionKey partitionKey)
        {
            return Instance.DeleteItemAsync<T>(id, partitionKey);
        }
    }
}
