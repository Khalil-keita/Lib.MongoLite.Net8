using MongoDB.Driver;

namespace Lib.MongoLite.Src.Core
{
    /// <summary>
    /// Contrat étendu pour le contexte MongoDB avec support avancé
    /// </summary>
    public interface IMongoDbContext : IDisposable
    {
        IMongoDatabase Database { get; }
        IMongoClient Client { get; }

        // Gestion des collections
        IMongoCollection<T> GetCollection<T>(string? name = null);

        string GetCollectionName<T>();

        // Sessions et transactions
        Task<IClientSessionHandle> StartSessionAsync(ClientSessionOptions? options = null);

        Task<T> ExecuteTransactionAsync<T>(Func<IClientSessionHandle, Task<T>> operation);

        Task ExecuteTransactionAsync(Func<IClientSessionHandle, Task> operation);

        Task<bool> HealthCheckAsync();

        // Bulk operations
        Task<BulkWriteResult<T>> BulkWriteAsync<T>(IEnumerable<WriteModel<T>> operations, BulkWriteOptions? options = null);
    }
}