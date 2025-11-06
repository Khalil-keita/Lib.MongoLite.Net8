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
[CollectionName("users")]
public class User 
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string Role { get; set; } = "User";
}

[CollectionName("posts")]
public class Post 
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
    public int Likes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsPublished { get; set; } = true;
}

[CollectionName("comments")]
public class Comment
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;
    public string PostId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// DTOs pour les résultats de jointure
public class UserWithPostsDto
{
    public User User { get; set; } = new();
    public List<Post> Posts { get; set; } = new();
    public int PostCount => Posts.Count;
    public int TotalLikes => Posts.Sum(p => p.Likes);
}

public class PostWithDetailsDto
{
    public Post Post { get; set; } = new();
    public User Author { get; set; } = new();
    public List<Comment> Comments { get; set; } = new();
    public int CommentCount => Comments.Count;
}
```


```C# 
public class UserService
{
    private readonly IRepository<User> _users;
    private readonly IRepository<Post> _posts;
    private readonly IRepository<Comment> _comments;
    private readonly IMongoDbContext _context;

    public UserService(
        IRepository<User> users, 
        IRepository<Post> posts,
        IRepository<Comment> comments,
        IMongoDbContext context)
    {
        _users = users;
        _posts = posts;
        _comments = comments;
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

    // JOINTURE SIMPLE - Users avec leurs Posts
    public async Task<List<UserWithPostsDto>> GetUsersWithPostsAsync()
    {
        var usersCollection = _context.GetCollection<User>();
        var postsCollection = _context.GetCollection<Post>();

        var results = await usersCollection.JoinAsync(
            foreignCollection: postsCollection,
            localKeySelector: user => user.Id,
            foreignKeySelector: post => post.UserId,
            resultSelector: (user, post) => new { User = user, Post = post }
        );

        // Regroupement par utilisateur
        return results
            .GroupBy(x => x.User.Id)
            .Select(g => new UserWithPostsDto
            {
                User = g.First().User,
                Posts = g.Select(x => x.Post).ToList()
            })
            .ToList();
    }

    // JOINTURE GAUCHE - Tous les Users avec leurs Posts (même ceux sans posts)
    public async Task<List<UserWithPostsDto>> GetAllUsersWithPostsAsync()
    {
        var usersCollection = _context.GetCollection<User>();
        var postsCollection = _context.GetCollection<Post>();

        var results = await usersCollection.LeftJoinAsync(
            foreignCollection: postsCollection,
            localKeySelector: user => user.Id,
            foreignKeySelector: post => post.UserId,
            resultSelector: (user, post) => new { User = user, Post = post }
        );

        return results
            .GroupBy(x => x.User.Id)
            .Select(g => new UserWithPostsDto
            {
                User = g.First().User,
                Posts = g.Where(x => x.Post != null).Select(x => x.Post!).ToList()
            })
            .ToList();
    }

    // LOOKUP AVEC AGREGATION - Posts avec Auteurs et Statistiques
    public async Task<List<PostWithDetailsDto>> GetPostsWithAuthorAndStatsAsync()
    {
        var postsCollection = _context.GetCollection<Post>();
        var usersCollection = _context.GetCollection<User>();
        var commentsCollection = _context.GetCollection<Comment>();

        // Jointure avec agrégation pour les statistiques
        var postsWithStats = await postsCollection.JoinWithAggregationAsync(
            foreignCollection: commentsCollection,
            localKeySelector: post => post.Id,
            foreignKeySelector: comment => comment.PostId,
            resultSelector: (post, comments) => new 
            { 
                Post = post, 
                CommentCount = comments.Count(),
                RecentComments = comments.OrderByDescending(c => c.CreatedAt).Take(5)
            },
            aggregation: comments => new 
            { 
                Count = comments.Count(),
                Recent = comments.OrderByDescending(c => c.CreatedAt).Take(5) 
            }
        );

        // Jointure simple pour les auteurs
        var users = await usersCollection.GetAllAsync();
        var usersDict = users.ToDictionary(u => u.Id, u => u);

        return postsWithStats.Select(result =>
        {
            usersDict.TryGetValue(result.Post.UserId, out var author);
            
            return new PostWithDetailsDto
            {
                Post = result.Post,
                Author = author ?? new User(),
                Comments = result.RecentComments.ToList(),
                CommentCount = result.CommentCount
            };
        }).ToList();
    }

    // JOINTURE COMPLEXE - Statistiques Utilisateurs
    public async Task<List<UserStatsDto>> GetUserStatisticsAsync()
    {
        var usersCollection = _context.GetCollection<User>();
        var postsCollection = _context.GetCollection<Post>();
        var commentsCollection = _context.GetCollection<Comment>();

        var userStats = await usersCollection.JoinWithAggregationAsync(
            foreignCollection: postsCollection,
            localKeySelector: user => user.Id,
            foreignKeySelector: post => post.UserId,
            resultSelector: (user, posts) => new UserStatsDto
            {
                UserId = user.Id,
                UserName = $"{user.FirstName} {user.LastName}",
                Email = user.Email,
                TotalPosts = posts.Count(),
                TotalLikes = posts.Sum(p => p.Likes),
                AvgLikesPerPost = posts.Any() ? (double)posts.Sum(p => p.Likes) / posts.Count() : 0,
                LastPostDate = posts.Any() ? posts.Max(p => p.CreatedAt) : null
            },
            aggregation: posts => new
            {
                PostCount = posts.Count(),
                TotalLikes = posts.Sum(p => p.Likes),
                LastPost = posts.Max(p => p.CreatedAt)
            }
        );

        return userStats.ToList();
    }

    // RECHERCHE AVEC JOINTURE - Recherche de posts avec infos auteur
    public async Task<List<PostWithDetailsDto>> SearchPostsWithAuthorsAsync(string searchTerm)
    {
        var postsCollection = _context.GetCollection<Post>();
        var usersCollection = _context.GetCollection<User>();

        // Filtre sur les posts
        var postsQuery = postsCollection.Query()
            .Where(p => p.IsPublished && 
                       (p.Title.Contains(searchTerm) || p.Content.Contains(searchTerm) || p.Tags.Contains(searchTerm)))
            .OrderByDescending(p => p.CreatedAt)
            .Limit(100);

        var filteredPosts = await postsQuery.ToListAsync();

        // Jointure avec les utilisateurs
        var authorIds = filteredPosts.Select(p => p.UserId).Distinct().ToList();
        var authors = await usersCollection.FindAsync(u => authorIds.Contains(u.Id));
        var authorsDict = authors.ToDictionary(u => u.Id, u => u);

        return filteredPosts.Select(post =>
        {
            authorsDict.TryGetValue(post.UserId, out var author);
            
            return new PostWithDetailsDto
            {
                Post = post,
                Author = author ?? new User()
            };
        }).ToList();
    }
}
```
