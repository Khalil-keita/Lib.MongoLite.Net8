# MongoLite.Core

**Une bibliothèque .NET simple et efficace pour MongoDB**

Une solution légère pour travailler avec MongoDB dans vos applications .NET, avec une API intuitive et des fonctionnalités essentielles.

## Fonctionnalités

- **Repository Pattern** générique
- **Query Builder** fluent
- **Transactions** ACID
- **Gestion automatique** des connexions
- **Configuration** simple

## Configuration

```C#
// appsettings.json
{
  "MongoDb": {
    "ConnectionString": "mongodb://localhost:27017",
    "DatabaseName": "MyApp",
    "MaxConnectionPoolSize": 100,
    "ConnectTimeout": 15
  }
}
```

```C#
// Program.cs
builder.Services.AddMongoLite(builder.Configuration);
```

### Utilisation
```C#
[BsonCollection("users")]
public class User 
{
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    
    // Relations
    public List<Post> Posts => HasMany<Post>();
    public Profile Profile => HasOne<Profile>();
}

[BsonCollection("posts")]
public class Post 
{
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    
    // Relation inverse
    public User Author => BelongsTo<User>();
}
```


```C# 
public class UserService
{
    private readonly IRepository<User> _users;
    private readonly IMongoDbContext _context;

    public UserService(IRepository<User> users, IMongoDbContext context)
    {
        _users = users;
        _context = context;
    }

    // CRUD Simple
    public async Task<User> CreateUserAsync(User user)
    {
        return await _users.CreateAsync(user);
    }

    // Query Avancée
    public async Task<List<User>> GetActiveUsersWithPostsAsync()
    {
        return await _context.GetCollection<User>()
            .Query()
            .Where(u => u.IsActive)
            .IncludeFields(u => u.Email)
            .IncludeFields(u => u.FirstName)
            .OrderByDescending(u => u.CreatedAt)
            .Limit(50)
            .ToListAsync();
    }

    // Transactions
    public async Task TransferUserDataAsync(string fromUserId, string toUserId)
    {
        await _context.ExecuteTransactionAsync(async session =>
        {
            var posts = await _context.GetCollection<Post>()
                .Find(session, p => p.UserId == fromUserId)
                .ToListAsync();

            var update = Builders<Post>.Update.Set(p => p.UserId, toUserId);
            await _context.GetCollection<Post>()
                .UpdateManyAsync(session, p => p.UserId == fromUserId, update);
        });
    }
}
```
