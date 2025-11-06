using MongoDB.Driver;
using System.Linq.Expressions;

namespace Lib.MongoLite.Src.Builder
{
    /// <summary>
    /// Interface définissant le contrat pour un constructeur de requêtes fluent et type-safe pour MongoDB
    /// </summary>
    /// <typeparam name="T">Type des entités sur lesquelles la requête s'exécute</typeparam>
    /// <remarks>
    /// Cette interface fournit une API fluide et intuitive pour construire des requêtes MongoDB
    /// de manière similaire à LINQ, avec support du typage fort et de l'intellisense.
    /// Toutes les méthodes retournent l'instance courante pour permettre le chaînage des appels.
    /// </remarks>
    public interface IQueryBuilder<T>
    {
        /// <summary>
        /// Ajoute une condition de filtrage à la requête
        /// </summary>
        /// <param name="predicate">Expression lambda définissant la condition de filtrage</param>
        /// <returns>Instance courante du constructeur pour le chaînage</returns>
        /// <example>
        /// <code>
        /// .Where(x => x.Age > 18)
        /// .Where(x => x.Name.Contains("John"))
        /// </code>
        /// </example>
        IQueryBuilder<T> Where(Expression<Func<T, bool>> predicate);

        /// <summary>
        /// Ajoute un tri ascendant sur le champ spécifié
        /// </summary>
        /// <typeparam name="TKey">Type du champ de tri</typeparam>
        /// <param name="keySelector">Expression lambda sélectionnant le champ de tri</param>
        /// <returns>Instance courante du constructeur pour le chaînage</returns>
        /// <example>
        /// <code>
        /// .OrderBy(x => x.Name)
        /// .OrderBy(x => x.CreatedDate)
        /// </code>
        /// </example>
        IQueryBuilder<T> OrderBy<TKey>(Expression<Func<T, TKey>> keySelector);

        /// <summary>
        /// Ajoute un tri descendant sur le champ spécifié
        /// </summary>
        /// <typeparam name="TKey">Type du champ de tri</typeparam>
        /// <param name="keySelector">Expression lambda sélectionnant le champ de tri</param>
        /// <returns>Instance courante du constructeur pour le chaînage</returns>
        /// <example>
        /// <code>
        /// .OrderByDescending(x => x.CreatedDate)
        /// .OrderByDescending(x => x.Score)
        /// </code>
        /// </example>
        IQueryBuilder<T> OrderByDescending<TKey>(Expression<Func<T, TKey>> keySelector);

        /// <summary>
        /// Ignore un nombre spécifié de documents au début du résultat
        /// </summary>
        /// <param name="count">Nombre de documents à ignorer</param>
        /// <returns>Instance courante du constructeur pour le chaînage</returns>
        /// <remarks>
        /// Utile pour la pagination en combinaison avec Limit()
        /// </remarks>
        /// <example>
        /// <code>
        /// .Skip(20) // Ignore les 20 premiers résultats
        /// .Limit(10) // Prend les 10 suivants (page 3 si page size = 10)
        /// </code>
        /// </example>
        IQueryBuilder<T> Skip(int count);

        /// <summary>
        /// Limite le nombre de documents retournés par la requête
        /// </summary>
        /// <param name="count">Nombre maximum de documents à retourner</param>
        /// <returns>Instance courante du constructeur pour le chaînage</returns>
        /// <example>
        /// <code>
        /// .Limit(100) // Retourne au maximum 100 documents
        /// </code>
        /// </example>
        IQueryBuilder<T> Limit(int count);

        /// <summary>
        /// Spécifie les champs à inclure dans les résultats (projection)
        /// </summary>
        /// <param name="fieldSelector">Expression lambda sélectionnant les champs à inclure</param>
        /// <returns>Instance courante du constructeur pour le chaînage</returns>
        /// <remarks>
        /// Améliore les performances en réduisant la quantité de données transférées
        /// </remarks>
        /// <example>
        /// <code>
        /// .IncludeFields(x => x.Name)
        /// .IncludeFields(x => x.Email)
        /// </code>
        /// </example>
        IQueryBuilder<T> IncludeFields(Expression<Func<T, object>> fieldSelector);

        /// <summary>
        /// Spécifie les champs à exclure des résultats (projection)
        /// </summary>
        /// <param name="fieldSelector">Expression lambda sélectionnant les champs à exclure</param>
        /// <returns>Instance courante du constructeur pour le chaînage</returns>
        /// <remarks>
        /// Utile pour exclure les champs sensibles ou volumineux
        /// </remarks>
        /// <example>
        /// <code>
        /// .ExcludeFields(x => x.PasswordHash)
        /// .ExcludeFields(x => x.AuditTrail)
        /// </code>
        /// </example>
        IQueryBuilder<T> ExcludeFields(Expression<Func<T, object>> fieldSelector);

        /// <summary>
        /// Exécute la requête et retourne tous les résultats sous forme de liste
        /// </summary>
        /// <returns>Liste des documents correspondants aux critères de la requête</returns>
        /// <example>
        /// <code>
        /// var users = await queryBuilder.ToListAsync();
        /// </code>
        /// </example>
        Task<List<T>> ToListAsync();

        /// <summary>
        /// Exécute la requête et retourne le premier résultat ou la valeur par défaut si aucun résultat
        /// </summary>
        /// <returns>Premier document correspondant ou default(T) si aucun résultat</returns>
        /// <remarks>
        /// Ne lance pas d'exception si aucun résultat n'est trouvé
        /// </remarks>
        /// <example>
        /// <code>
        /// var user = await queryBuilder.FirstOrDefaultAsync();
        /// if (user != null) { ... }
        /// </code>
        /// </example>
        Task<T?> FirstOrDefaultAsync();

        /// <summary>
        /// Exécute la requête et retourne le premier résultat
        /// </summary>
        /// <returns>Premier document correspondant aux critères</returns>
        /// <exception cref="InvalidOperationException">Lancée si aucun résultat n'est trouvé</exception>
        /// <example>
        /// <code>
        /// try {
        ///     var user = await queryBuilder.FirstAsync();
        /// } catch (InvalidOperationException) {
        ///     // Aucun résultat trouvé
        /// }
        /// </code>
        /// </example>
        Task<T> FirstAsync();

        /// <summary>
        /// Compte le nombre total de documents correspondants aux critères de filtrage
        /// </summary>
        /// <returns>Nombre de documents correspondants</returns>
        /// <example>
        /// <code>
        /// var total = await queryBuilder.CountAsync();
        /// Console.WriteLine($"Found {total} documents");
        /// </code>
        /// </example>
        Task<long> CountAsync();

        /// <summary>
        /// Vérifie si au moins un document correspond aux critères de filtrage
        /// </summary>
        /// <returns>True si au moins un document correspond, sinon False</returns>
        /// <example>
        /// <code>
        /// var exists = await queryBuilder.AnyAsync();
        /// if (exists) { ... }
        /// </code>
        /// </example>
        Task<bool> AnyAsync();

        /// <summary>
        /// Calcule la valeur maximale d'un champ spécifié parmi les documents correspondants
        /// </summary>
        /// <typeparam name="TResult">Type de la valeur de retour</typeparam>
        /// <param name="selector">Expression lambda sélectionnant le champ à analyser</param>
        /// <returns>Valeur maximale du champ spécifié</returns>
        /// <example>
        /// <code>
        /// var maxAge = await queryBuilder.MaxAsync(x => x.Age);
        /// var latestDate = await queryBuilder.MaxAsync(x => x.CreatedDate);
        /// </code>
        /// </example>
        Task<TResult> MaxAsync<TResult>(Expression<Func<T, TResult>> selector);

        /// <summary>
        /// Calcule la valeur minimale d'un champ spécifié parmi les documents correspondants
        /// </summary>
        /// <typeparam name="TResult">Type de la valeur de retour</typeparam>
        /// <param name="selector">Expression lambda sélectionnant le champ à analyser</param>
        /// <returns>Valeur minimale du champ spécifié</returns>
        /// <example>
        /// <code>
        /// var minAge = await queryBuilder.MinAsync(x => x.Age);
        /// var earliestDate = await queryBuilder.MinAsync(x => x.CreatedDate);
        /// </code>
        /// </example>
        Task<TResult> MinAsync<TResult>(Expression<Func<T, TResult>> selector);
    }
}