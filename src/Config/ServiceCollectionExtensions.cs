using System.Linq.Expressions;
using Lib.MongoLite.Src.Builder;
using Lib.MongoLite.Src.Config;
using Lib.MongoLite.Src.Core;
using Lib.MongoLite.Src.Repository;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;

namespace Lib.MongoLite.src.Config
{
    /// <summary>
    /// Méthodes d'extension pour l'intégration de MongoLite dans le système de dépendance .NET
    /// </summary>
    /// <remarks>
    /// Cette classe fournit des méthodes pour configurer et enregistrer les services MongoLite
    /// dans le conteneur d'injection de dépendances.
    /// </remarks>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Enregistre tous les services nécessaires pour MongoLite dans le conteneur DI
        /// </summary>
        /// <param name="services">Collection des services à étendre</param>
        /// <param name="configuration">Configuration de l'application contenant les paramètres MongoDB</param>
        /// <param name="sectionName">Nom de la section de configuration pour MongoDB (défaut: "MongoDb")</param>
        /// <returns>Collection des services pour le chaînage des appels</returns>
        /// <example>
        /// <code>
        /// // Dans Program.cs d'une application ASP.NET Core
        /// var builder = WebApplication.CreateBuilder(args);
        /// builder.Services.AddMongoLite(builder.Configuration);
        ///
        /// // Ou avec une section personnalisée
        /// builder.Services.AddMongoLite(builder.Configuration, "Database");
        /// </code>
        /// </example>
        public static IServiceCollection AddMongoLite(
            this IServiceCollection services,
            IConfiguration configuration,
            string sectionName = "MongoDb")
        {
            // Configuration des options MongoDB à partir de la section de configuration
            // Les options seront disponibles via IOptions<MongoDbOptions> dans toute l'application
            services.Configure<MongoDbOptions>(configuration.GetSection(sectionName));

            // Enregistrement du contexte MongoDB comme singleton
            // Un seul instance sera créée et réutilisée dans toute l'application
            services.AddSingleton<IMongoDbContext, MongoDbContext>()

            // Enregistrement automatique des repositories génériques
            // Permet d'injecter IRepository<T> directement sans configuration supplémentaire
            .AddScoped(typeof(IRepository<>), typeof(Repository<>));

            return services;
        }

        /// <summary>
        /// Enregistre un repository spécifique pour un type d'entité donné
        /// </summary>
        /// <typeparam name="T">Type de l'entité pour laquelle enregistrer le repository</typeparam>
        /// <param name="services">Collection des services à étendre</param>
        /// <returns>Collection des services pour le chaînage des appels</returns>
        /// <remarks>
        /// Cette méthode est utile pour l'enregistrement explicite de repositories spécifiques
        /// ou pour override l'enregistrement générique automatique
        /// </remarks>
        /// <example>
        /// <code>
        /// // Enregistrement explicite d'un repository User
        /// services.AddMongoRepository<User>();
        ///
        /// // Injection dans un service
        /// public class UserService
        /// {
        ///     public UserService(IRepository<User> users) { ... }
        /// }
        /// </code>
        /// </example>
        public static IServiceCollection AddMongoRepository<T>(
            this IServiceCollection services) where T : class
        {
            services.AddScoped<IRepository<T>, Repository<T>>();
            return services;
        }
    }

    /// <summary>
    /// Méthodes d'extension pour les collections MongoDB permettant une syntaxe fluide et intuitive
    /// </summary>
    /// <remarks>
    /// Ces extensions fournissent une API de requête fluide similaire à LINQ
    /// pour interagir avec les collections MongoDB
    /// </remarks>
    public static class QueryableExtensions
    {
        /// <summary>
        /// Démarre la construction d'une requête fluide sur une collection MongoDB
        /// </summary>
        /// <typeparam name="T">Type des documents dans la collection</typeparam>
        /// <param name="collection">Collection MongoDB sur laquelle exécuter la requête</param>
        /// <returns>Instance du constructeur de requête pour le chaînage des méthodes</returns>
        /// <example>
        /// <code>
        /// var activeUsers = await _context.GetCollection<User>()
        ///     .Query()
        ///     .Where(u => u.IsActive)
        ///     .OrderBy(u => u.Name)
        ///     .Limit(10)
        ///     .ToListAsync();
        /// </code>
        /// </example>
        public static IQueryBuilder<T> Query<T>(this IMongoCollection<T> collection)
        {
            return new QueryBuilder<T>(collection);
        }

        /// <summary>
        /// Démarre la construction d'une requête avec une condition de filtrage initiale
        /// </summary>
        /// <typeparam name="T">Type des documents dans la collection</typeparam>
        /// <param name="collection">Collection MongoDB sur laquelle exécuter la requête</param>
        /// <param name="predicate">Expression lambda définissant la condition de filtrage</param>
        /// <returns>Instance du constructeur de requête avec le filtre appliqué</returns>
        /// <example>
        /// <code>
        /// var adminUsers = await _context.GetCollection<User>()
        ///     .Where(u => u.Role == "Admin")
        ///     .OrderBy(u => u.CreatedAt)
        ///     .ToListAsync();
        /// </code>
        /// </example>
        public static IQueryBuilder<T> Where<T>(this IMongoCollection<T> collection, Expression<Func<T, bool>> predicate)
        {
            return new QueryBuilder<T>(collection).Where(predicate);
        }
    }
}