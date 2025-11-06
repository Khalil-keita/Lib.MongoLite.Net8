using System.Linq.Expressions;
using Lib.MongoLite.Src.Core;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace Lib.MongoLite.Src.Repository
{
    public sealed class Repository<T>(IMongoDbContext context) : IRepository<T> where T : class
    {
        private readonly IMongoCollection<T> _collection = context.GetCollection<T>();

        public async Task<T?> GetByIdAsync(string id)
        {
            var filter = Builders<T>.Filter.Eq("_id", ObjectId.Parse(id));
            return await _collection.Find(filter).FirstOrDefaultAsync();
        }

        public async Task<IEnumerable<T>> GetAllAsync()
        {
            return await _collection.Find(_ => true).ToListAsync();
        }

        public async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate)
        {
            return await _collection.Find(predicate).ToListAsync();
        }

        public async Task<T> CreateAsync(T entity)
        {
            await _collection.InsertOneAsync(entity);
            return entity;
        }

        public async Task<T> UpdateAsync(string id, T entity)
        {
            var filter = Builders<T>.Filter.Eq("_id", ObjectId.Parse(id));
            var options = new ReplaceOptions { IsUpsert = false };

            var result = await _collection.ReplaceOneAsync(filter, entity, options);
            if (result.MatchedCount == 0)
                throw new InvalidOperationException($"Entity with id {id} not found");

            return entity;
        }

        public async Task<bool> DeleteAsync(string id)
        {
            var filter = Builders<T>.Filter.Eq("_id", ObjectId.Parse(id));
            var result = await _collection.DeleteOneAsync(filter);

            if (result.DeletedCount > 0)
            {
                return true;
            }

            return false;
        }

        public async Task<long> CountAsync(Expression<Func<T, bool>>? predicate = null)
        {
            var filter = predicate != null ? Builders<T>.Filter.Where(predicate) : Builders<T>.Filter.Empty;
            return await _collection.CountDocumentsAsync(filter);
        }

        public async Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate)
        {
            return await _collection.Find(predicate).AnyAsync();
        }

        #region Implémentation Optimisée des Méthodes de Jointure

        public async Task<IEnumerable<TResult>> JoinAsync<TForeign, TResult>(
            IMongoCollection<TForeign> foreignCollection,
            Expression<Func<T, object>> localKeySelector,
            Expression<Func<TForeign, object>> foreignKeySelector,
            Expression<Func<T, TForeign, TResult>> resultSelector)
        {
            // Utilisation de projections pour réduire les données transférées
            var localKeyProjection = Builders<T>.Projection.Expression(localKeySelector);
            var foreignKeyProjection = Builders<TForeign>.Projection.Expression(foreignKeySelector);

            var localEntitiesTask = _collection.Find(_ => true)
                .Project(localKeyProjection)
                .ToListAsync();

            var foreignEntitiesTask = foreignCollection.Find(_ => true)
                .Project(foreignKeyProjection)
                .ToListAsync();

            await Task.WhenAll(localEntitiesTask, foreignEntitiesTask).ConfigureAwait(false);

            var localKeys = await localEntitiesTask;
            var foreignKeys = await foreignEntitiesTask;

            // Filtrage côté base de données pour ne récupérer que les entités nécessaires
            var filteredForeignEntities = await foreignCollection
                .Find(Builders<TForeign>.Filter.In(foreignKeySelector, localKeys.Distinct()))
                .ToListAsync()
                .ConfigureAwait(false);

            var localEntities = await _collection
                .Find(Builders<T>.Filter.In(localKeySelector, foreignKeys.Distinct()))
                .ToListAsync()
                .ConfigureAwait(false);

            var compiledResultSelector = resultSelector.Compile();
            return localEntities
                .Join(filteredForeignEntities,
                    localKeySelector.Compile(),
                    foreignKeySelector.Compile(),
                    compiledResultSelector);
        }

        public async Task<IEnumerable<TResult>> LeftJoinAsync<TForeign, TResult>(
            IMongoCollection<TForeign> foreignCollection,
            Expression<Func<T, object>> localKeySelector,
            Expression<Func<TForeign, object>> foreignKeySelector,
            Expression<Func<T, TForeign?, TResult>> resultSelector)
        {
            // Récupération de toutes les clés locales
            var localKeys = await _collection.Find(_ => true)
                .Project(Builders<T>.Projection.Expression(localKeySelector))
                .ToListAsync()
                .ConfigureAwait(false);

            // Récupération des entités étrangères correspondant aux clés locales
            var foreignEntitiesTask = foreignCollection
                .Find(Builders<TForeign>.Filter.In(foreignKeySelector, localKeys.Distinct()))
                .ToListAsync();

            // Récupération de toutes les entités locales
            var localEntitiesTask = _collection.Find(_ => true)
                .ToListAsync();

            await Task.WhenAll(foreignEntitiesTask, localEntitiesTask).ConfigureAwait(false);

            var foreignEntities = await foreignEntitiesTask;
            var localEntities = await localEntitiesTask;

            var compiledResultSelector = resultSelector.Compile();

            return localEntities
                .GroupJoin(foreignEntities,
                    localKeySelector.Compile(),
                    foreignKeySelector.Compile(),
                    (local, foreigns) => new { Local = local, Foreigns = foreigns })
                .SelectMany(
                    x => x.Foreigns.DefaultIfEmpty(),
                    (x, foreign) => compiledResultSelector(x.Local, foreign));
        }

        public async Task<IEnumerable<T>> LookupAsync<TForeign>(
            IMongoCollection<TForeign> foreignCollection,
            Expression<Func<T, object>> localKeySelector,
            Expression<Func<TForeign, object>> foreignKeySelector,
            string resultPropertyName)
        {
            // Implémentation utilisant le pipeline d'agrégation MongoDB
            var pipeline = new[]
            {
                new BsonDocument("$lookup", new BsonDocument
                {
                    { "from", foreignCollection.CollectionNamespace.CollectionName },
                    { "localField", GetFieldName(localKeySelector) },
                    { "foreignField", GetFieldName(foreignKeySelector) },
                    { "as", resultPropertyName }
                })
            };

            var results = await _collection.Aggregate<T>(pipeline)
                .ToListAsync()
                .ConfigureAwait(false);

            return results;
        }

        public async Task<IEnumerable<TResult>> JoinWithAggregationAsync<TForeign, TResult>(
            IMongoCollection<TForeign> foreignCollection,
            Expression<Func<T, object>> localKeySelector,
            Expression<Func<TForeign, object>> foreignKeySelector,
            Expression<Func<T, IEnumerable<TForeign>, TResult>> resultSelector,
            Func<IEnumerable<TForeign>, object>? aggregation = null)
        {
            // Utilisation de l'agrégation MongoDB pour les jointures complexes
            var localField = GetFieldName(localKeySelector);
            var foreignField = GetFieldName(foreignKeySelector);

            var pipelineStages = new List<BsonDocument>
            {
                new("$lookup", new BsonDocument
                {
                    { "from", foreignCollection.CollectionNamespace.CollectionName },
                    { "localField", localField },
                    { "foreignField", foreignField },
                    { "as", "joinedData" }
                })
            };

            // Application de l'agrégation si spécifiée
            if (aggregation != null)
            {
                pipelineStages.Add(new BsonDocument("$addFields", new BsonDocument
                {
                    { "joinedData", new BsonDocument("$function", new BsonDocument
                        {
                            { "body", aggregation?.ToString() }, // Simplifié
                            { "args", new BsonArray { "$joinedData" } },
                            { "lang", "js" }
                        })
                    }
                }));
            }

            // Exécution du pipeline d'agrégation
            var aggregationResult = await _collection.Aggregate<BsonDocument>(pipelineStages)
                .ToListAsync()
                .ConfigureAwait(false);

            // Conversion des résultats (nécessite une logique de mapping spécifique)
            return aggregationResult.Select(doc =>
            {
                var localEntity = BsonSerializer.Deserialize<T>(doc);
                var foreignEntities = doc["joinedData"].AsBsonArray
                    .Select(x => BsonSerializer.Deserialize<TForeign>(x.AsBsonDocument))
                    .ToList();

                return resultSelector.Compile()(localEntity, foreignEntities);
            });
        }

        // Méthode utilitaire pour obtenir le nom du champ à partir d'une expression
        private static string GetFieldName<TEntity, TProperty>(Expression<Func<TEntity, TProperty>> expression)
        {
            if (expression.Body is MemberExpression memberExpression)
            {
                return memberExpression.Member.Name;
            }

            if (expression.Body is UnaryExpression unaryExpression &&
                unaryExpression.NodeType == ExpressionType.Convert &&
                unaryExpression.Operand is MemberExpression operand)
            {
                return operand.Member.Name;
            }

            throw new ArgumentException("Expression must be a member access", nameof(expression));
        }

        #endregion Implémentation Optimisée des Méthodes de Jointure
    }
}