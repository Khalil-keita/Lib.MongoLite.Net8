using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using MongoDB.Driver;

namespace Lib.MongoLite.Src.Repository
{
    /// <summary>
    /// Interface définissant le contrat générique pour les opérations de repository sur les entités MongoDB
    /// </summary>
    /// <typeparam name="T">Type de l'entité gérée par le repository</typeparam>
    /// <remarks>
    /// Cette interface fournit une abstraction pour les opérations CRUD de base
    /// et les requêtes courantes sur les collections MongoDB.
    /// Elle suit le pattern Repository pour découpler la logique métier de l'accès aux données.
    /// </remarks>
    public interface IRepository<T> where T : class
    {
        /// <summary>
        /// Récupère une entité par son identifiant unique
        /// </summary>
        /// <param name="id">Identifiant de l'entité à récupérer</param>
        /// <returns>L'entité correspondante ou null si non trouvée</returns>
        /// <example>
        /// <code>
        /// var user = await userRepository.GetByIdAsync("507f1f77bcf86cd799439011");
        /// </code>
        /// </example>
        Task<T?> GetByIdAsync(string id);

        /// <summary>
        /// Récupère toutes les entités de la collection
        /// </summary>
        /// <returns>Collection de toutes les entités</returns>
        /// <remarks>
        /// À utiliser avec précaution sur les grandes collections.
        /// Préférez les méthodes de pagination pour les datasets volumineux.
        /// </remarks>
        /// <example>
        /// <code>
        /// var allUsers = await userRepository.GetAllAsync();
        /// </code>
        /// </example>
        Task<IEnumerable<T>> GetAllAsync();

        /// <summary>
        /// Recherche des entités selon un prédicat de filtrage
        /// </summary>
        /// <param name="predicate">Expression lambda définissant les conditions de recherche</param>
        /// <returns>Collection des entités correspondant aux critères</returns>
        /// <example>
        /// <code>
        /// var activeUsers = await userRepository.FindAsync(u => u.IsActive);
        /// var adminUsers = await userRepository.FindAsync(u => u.Role == "Admin");
        /// </code>
        /// </example>
        Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate);

        /// <summary>
        /// Crée une nouvelle entité dans la collection
        /// </summary>
        /// <param name="entity">Entité à créer</param>
        /// <returns>L'entité créée avec son identifiant généré</returns>
        /// <example>
        /// <code>
        /// var newUser = new User { Name = "John", Email = "john@example.com" };
        /// var createdUser = await userRepository.CreateAsync(newUser);
        /// Console.WriteLine(createdUser.Id); // Affiche l'ID généré
        /// </code>
        /// </example>
        Task<T> CreateAsync(T entity);

        /// <summary>
        /// Met à jour une entité existante
        /// </summary>
        /// <param name="id">Identifiant de l'entité à mettre à jour</param>
        /// <param name="entity">Nouvelles données de l'entité</param>
        /// <returns>L'entité mise à jour</returns>
        /// <exception cref="InvalidOperationException">Lancée si l'entité n'existe pas</exception>
        /// <example>
        /// <code>
        /// var userToUpdate = await userRepository.GetByIdAsync(userId);
        /// userToUpdate.Name = "Jane";
        /// var updatedUser = await userRepository.UpdateAsync(userId, userToUpdate);
        /// </code>
        /// </example>
        Task<T> UpdateAsync(string id, T entity);

        /// <summary>
        /// Supprime une entité par son identifiant
        /// </summary>
        /// <param name="id">Identifiant de l'entité à supprimer</param>
        /// <returns>True si l'entité a été supprimée, False si elle n'existait pas</returns>
        /// <example>
        /// <code>
        /// var isDeleted = await userRepository.DeleteAsync("507f1f77bcf86cd799439011");
        /// if (isDeleted) {
        ///     Console.WriteLine("User deleted successfully");
        /// }
        /// </code>
        /// </example>
        Task<bool> DeleteAsync(string id);

        /// <summary>
        /// Compte le nombre d'entités correspondant éventuellement à un prédicat
        /// </summary>
        /// <param name="predicate">Expression lambda de filtrage (optionnelle)</param>
        /// <returns>Nombre total d'entités correspondantes</returns>
        /// <example>
        /// <code>
        /// var totalUsers = await userRepository.CountAsync();
        /// var activeUsersCount = await userRepository.CountAsync(u => u.IsActive);
        /// </code>
        /// </example>
        Task<long> CountAsync(Expression<Func<T, bool>>? predicate = null);

        /// <summary>
        /// Vérifie si au moins une entité correspond au prédicat spécifié
        /// </summary>
        /// <param name="predicate">Expression lambda définissant les conditions de vérification</param>
        /// <returns>True si au moins une entité correspond, sinon False</returns>
        /// <example>
        /// <code>
        /// var hasAdmins = await userRepository.ExistsAsync(u => u.Role == "Admin");
        /// var emailExists = await userRepository.ExistsAsync(u => u.Email == "test@example.com");
        /// </code>
        /// </example>
        Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate);

        #region Méthodes de Jointure

        /// <summary>
        /// Effectue une jointure entre deux collections avec un champ de liaison spécifié
        /// </summary>
        /// <typeparam name="TForeign">Type de l'entité étrangère à joindre</typeparam>
        /// <typeparam name="TResult">Type du résultat de la jointure</typeparam>
        /// <param name="foreignCollection">Collection étrangère à joindre</param>
        /// <param name="localKeySelector">Clé locale pour la jointure</param>
        /// <param name="foreignKeySelector">Clé étrangère pour la jointure</param>
        /// <param name="resultSelector">Sélecteur pour créer le résultat de la jointure</param>
        /// <returns>Collection des résultats de la jointure</returns>
        /// <example>
        /// <code>
        /// var usersWithPosts = await userRepository.JoinAsync(
        ///     postRepository.GetCollection(),
        ///     user => user.Id,
        ///     post => post.UserId,
        ///     (user, post) => new { User = user, Post = post }
        /// );
        /// </code>
        /// </example>
        Task<IEnumerable<TResult>> JoinAsync<TForeign, TResult>(
            IMongoCollection<TForeign> foreignCollection,
            Expression<Func<T, object>> localKeySelector,
            Expression<Func<TForeign, object>> foreignKeySelector,
            Expression<Func<T, TForeign, TResult>> resultSelector);

        /// <summary>
        /// Effectue une jointure gauche (left join) entre deux collections
        /// </summary>
        /// <typeparam name="TForeign">Type de l'entité étrangère à joindre</typeparam>
        /// <typeparam name="TResult">Type du résultat de la jointure</typeparam>
        /// <param name="foreignCollection">Collection étrangère à joindre</param>
        /// <param name="localKeySelector">Clé locale pour la jointure</param>
        /// <param name="foreignKeySelector">Clé étrangère pour la jointure</param>
        /// <param name="resultSelector">Sélecteur pour créer le résultat de la jointure</param>
        /// <returns>Collection des résultats de la jointure gauche</returns>
        /// <remarks>
        /// Les entités locales sans correspondance dans la collection étrangère
        /// seront incluses avec une valeur null pour la partie étrangère
        /// </remarks>
        /// <example>
        /// <code>
        /// var allUsersWithPosts = await userRepository.LeftJoinAsync(
        ///     postRepository.GetCollection(),
        ///     user => user.Id,
        ///     post => post.UserId,
        ///     (user, post) => new { User = user, Post = post }
        /// );
        /// </code>
        /// </example>
        Task<IEnumerable<TResult>> LeftJoinAsync<TForeign, TResult>(
            IMongoCollection<TForeign> foreignCollection,
            Expression<Func<T, object>> localKeySelector,
            Expression<Func<TForeign, object>> foreignKeySelector,
            Expression<Func<T, TForeign?, TResult>> resultSelector);

        /// <summary>
        /// Effectue une jointure avec regroupement (Lookup) similaire à $lookup d'MongoDB
        /// </summary>
        /// <typeparam name="TForeign">Type de l'entité étrangère à joindre</typeparam>
        /// <param name="foreignCollection">Collection étrangère à joindre</param>
        /// <param name="localKeySelector">Clé locale pour la jointure</param>
        /// <param name="foreignKeySelector">Clé étrangère pour la jointure</param>
        /// <param name="resultPropertyName">Nom de la propriété pour stocker les résultats joints</param>
        /// <returns>Collection des entités locales avec les entités étrangères jointes</returns>
        /// <example>
        /// <code>
        /// var usersWithPosts = await userRepository.LookupAsync(
        ///     postRepository.GetCollection(),
        ///     user => user.Id,
        ///     post => post.UserId,
        ///     "Posts"
        /// );
        /// // Chaque user aura une propriété Posts contenant ses articles
        /// </code>
        /// </example>
        Task<IEnumerable<T>> LookupAsync<TForeign>(
            IMongoCollection<TForeign> foreignCollection,
            Expression<Func<T, object>> localKeySelector,
            Expression<Func<TForeign, object>> foreignKeySelector,
            string resultPropertyName);

        /// <summary>
        /// Effectue une jointure avec agrégation et transformation des résultats
        /// </summary>
        /// <typeparam name="TForeign">Type de l'entité étrangère à joindre</typeparam>
        /// <typeparam name="TResult">Type du résultat de la jointure</typeparam>
        /// <param name="foreignCollection">Collection étrangère à joindre</param>
        /// <param name="localKeySelector">Clé locale pour la jointure</param>
        /// <param name="foreignKeySelector">Clé étrangère pour la jointure</param>
        /// <param name="resultSelector">Sélecteur pour créer le résultat de la jointure</param>
        /// <param name="aggregation">Fonction d'agrégation optionnelle sur les résultats joints</param>
        /// <returns>Collection des résultats de la jointure agrégée</returns>
        /// <example>
        /// <code>
        /// var usersWithPostCount = await userRepository.JoinWithAggregationAsync(
        ///     postRepository.GetCollection(),
        ///     user => user.Id,
        ///     post => post.UserId,
        ///     (user, posts) => new { User = user, PostCount = posts.Count() },
        ///     posts => posts.Count()
        /// );
        /// </code>
        /// </example>
        Task<IEnumerable<TResult>> JoinWithAggregationAsync<TForeign, TResult>(
            IMongoCollection<TForeign> foreignCollection,
            Expression<Func<T, object>> localKeySelector,
            Expression<Func<TForeign, object>> foreignKeySelector,
            Expression<Func<T, IEnumerable<TForeign>, TResult>> resultSelector,
            Func<IEnumerable<TForeign>, object>? aggregation = null);

        #endregion Méthodes de Jointure
    }
}