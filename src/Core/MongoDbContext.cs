using System.Reflection;
using System.Text;
using Lib.MongoLite.Src.Attributes;
using Lib.MongoLite.Src.Config;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Driver;
using MongoDB.Driver.Core.Events;

namespace Lib.MongoLite.Src.Core
{
    public sealed class MongoDbContext : IMongoDbContext
    {
        private readonly IMongoDatabase _database;
        private readonly ILogger<MongoDbContext> _logger;
        private readonly MongoDbOptions _options;
        private bool _disposed = false;

        public IMongoDatabase Database => _database;
        public IMongoClient Client { get; }

        public MongoDbContext(IOptions<MongoDbOptions> options, ILogger<MongoDbContext> logger)
        {
            _logger = logger;
            _options = options.Value;

            RegisterConventions();

            var settings = ConfigureClientSettings();
            Client = new MongoClient(settings);
            _database = Client.GetDatabase(_options.DatabaseName);

            _logger.LogInformation("MongoDB context initialized for database: {Database}", _options.DatabaseName);
        }

        private static void RegisterConventions()
        {
            var pack = new ConventionPack
            {
                new CamelCaseElementNameConvention(),
                new IgnoreExtraElementsConvention(true),
                new EnumRepresentationConvention(BsonType.String)
            };
            ConventionRegistry.Register("MongoLiteConventions", pack, t => true);
        }

        private MongoClientSettings ConfigureClientSettings()
        {
            var settings = MongoClientSettings.FromConnectionString(_options.ConnectionString);

            // Optimisations performances
            settings.MaxConnectionPoolSize = _options.MaxConnectionPoolSize ?? 100;
            settings.MinConnectionPoolSize = _options.MinConnectionPoolSize ?? 10;
            settings.ConnectTimeout = _options.ConnectTimeout ?? TimeSpan.FromSeconds(15);
            settings.SocketTimeout = _options.SocketTimeout ?? TimeSpan.FromSeconds(30);
            settings.ServerSelectionTimeout = _options.ServerSelectionTimeout ?? TimeSpan.FromSeconds(30);

            // Résilience
            settings.RetryReads = true;
            settings.RetryWrites = true;
            settings.ReadConcern = ReadConcern.Majority;
            settings.WriteConcern = WriteConcern.WMajority;

            // Logging en debug
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                settings.ClusterConfigurator = cb =>
                {
                    cb.Subscribe<CommandStartedEvent>(e =>
                        _logger.LogDebug("MongoDB Command: {CommandName}", e.CommandName));
                };
            }

            return settings;
        }

        public IMongoCollection<T> GetCollection<T>(string? name = null)
        {
            var collectionName = name ?? GetCollectionName<T>();
            return _database.GetCollection<T>(collectionName);
        }

        public string GetCollectionName<T>()
        {
            var type = typeof(T);
            var attribute = type.GetCustomAttributes<CollectionName>(true)
                              .FirstOrDefault();
            var name = attribute?.Name ?? type.Name + "s";
            return ConvertToSnakeCase(name);
        }

        public async Task<IClientSessionHandle> StartSessionAsync(ClientSessionOptions? options = null)
        {
            options ??= new ClientSessionOptions
            {
                CausalConsistency = true,
                DefaultTransactionOptions = new TransactionOptions(
                    ReadConcern.Majority,
                    ReadPreference.Primary,
                    WriteConcern.WMajority
                )
            };

            return await Client.StartSessionAsync(options);
        }

        public async Task<T> ExecuteTransactionAsync<T>(Func<IClientSessionHandle, Task<T>> operation)
        {
            using var session = await StartSessionAsync();
            session.StartTransaction();

            try
            {
                var result = await operation(session);
                await session.CommitTransactionAsync();
                return result;
            }
            catch
            {
                await session.AbortTransactionAsync();
                throw;
            }
        }

        public async Task ExecuteTransactionAsync(Func<IClientSessionHandle, Task> operation)
        {
            await ExecuteTransactionAsync<object?>(async session =>
            {
                await operation(session);
                return null;
            });
        }

        public async Task<bool> HealthCheckAsync()
        {
            try
            {
                await _database.RunCommandAsync<BsonDocument>("{ping:1}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "MongoDB health check failed");
                return false;
            }
        }

        public async Task<BulkWriteResult<T>> BulkWriteAsync<T>(
            IEnumerable<WriteModel<T>> operations,
            BulkWriteOptions? options = null)
        {
            var collection = GetCollection<T>();
            return await collection.BulkWriteAsync(operations, options);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                _logger.LogDebug("MongoDB context disposed");
            }
        }

        private static string ConvertToSnakeCase(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            var result = new StringBuilder();
            result.Append(char.ToLowerInvariant(input[0]));

            for (int i = 1; i < input.Length; i++)
            {
                if (char.IsUpper(input[i]))
                {
                    result.Append('_');
                    result.Append(char.ToLowerInvariant(input[i]));
                }
                else
                {
                    result.Append(input[i]);
                }
            }

            return result.ToString();
        }
    }
}