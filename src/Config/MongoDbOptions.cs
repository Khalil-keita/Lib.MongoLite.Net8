namespace Lib.MongoLite.Src.Config
{
    public class MongoDbOptions
    {
        public string ConnectionString { get; set; } = string.Empty;
        public string DatabaseName { get; set; } = string.Empty;
        public int? MaxConnectionPoolSize { get; set; }
        public int? MinConnectionPoolSize { get; set; }
        public TimeSpan? ConnectTimeout { get; set; }
        public TimeSpan? SocketTimeout { get; set; }
        public TimeSpan? ServerSelectionTimeout { get; set; }
    }
}