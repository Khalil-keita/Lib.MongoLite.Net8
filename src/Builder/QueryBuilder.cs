using System.Linq.Expressions;
using Lib.MongoLite.Src.Builder;
using MongoDB.Driver;

namespace Lib.MongoLite.Src.Builder
{
    public sealed class QueryBuilder<T>(IMongoCollection<T> collection) : IQueryBuilder<T>
    {
        private readonly IMongoCollection<T> _collection = collection;
        private FilterDefinition<T> _filter = Builders<T>.Filter.Empty;
        private SortDefinition<T>? _sort;
        private int? _skip;
        private int? _limit;
        private ProjectionDefinition<T>? _projection;

        public IQueryBuilder<T> Where(Expression<Func<T, bool>> predicate)
        {
            _filter &= Builders<T>.Filter.Where(predicate);
            return this;
        }

        public IQueryBuilder<T> OrderBy<TKey>(Expression<Func<T, TKey>> keySelector)
        {
            var fieldDefinition = new ExpressionFieldDefinition<T>(keySelector);

            _sort = _sort == null
                ? Builders<T>.Sort.Ascending(fieldDefinition)
                : _sort.Ascending(fieldDefinition);
            return this;
        }

        public IQueryBuilder<T> OrderByDescending<TKey>(Expression<Func<T, TKey>> keySelector)
        {
            var fieldDefinition = new ExpressionFieldDefinition<T>(keySelector);

            _sort = _sort == null
                ? Builders<T>.Sort.Descending(fieldDefinition)
                : _sort.Descending(fieldDefinition);
            return this;
        }

        public IQueryBuilder<T> Skip(int count)
        {
            _skip = count;
            return this;
        }

        public IQueryBuilder<T> Limit(int count)
        {
            _limit = count;
            return this;
        }

        public IQueryBuilder<T> IncludeFields(Expression<Func<T, object>> fieldSelector)
        {
            var projection = Builders<T>.Projection.Include(fieldSelector);
            _projection = _projection == null ? projection : Builders<T>.Projection.Combine(_projection, projection);
            return this;
        }

        public IQueryBuilder<T> ExcludeFields(Expression<Func<T, object>> fieldSelector)
        {
            var projection = Builders<T>.Projection.Exclude(fieldSelector);
            _projection = _projection == null ? projection : Builders<T>.Projection.Combine(_projection, projection);
            return this;
        }

        public async Task<List<T>> ToListAsync()
        {
            var findFluent = _collection.Find(_filter);

            // Application des options une par une
            if (_sort != null)
                findFluent = findFluent.Sort(_sort);

            if (_skip.HasValue)
                findFluent = findFluent.Skip(_skip.Value);

            if (_limit.HasValue)
                findFluent = findFluent.Limit(_limit.Value);

            if (_projection != null)
                findFluent = findFluent.Project<T>(_projection);

            return await findFluent.ToListAsync();
        }

        public async Task<T?> FirstOrDefaultAsync()
        {
            return await _collection.Find(_filter).FirstOrDefaultAsync();
        }

        public async Task<T> FirstAsync()
        {
            var result = await FirstOrDefaultAsync();
            return result ?? throw new InvalidOperationException("Sequence contains no elements");
        }

        public async Task<long> CountAsync()
        {
            return await _collection.CountDocumentsAsync(_filter);
        }

        public async Task<bool> AnyAsync()
        {
            return await _collection.Find(_filter).AnyAsync();
        }

        public async Task<TResult> MaxAsync<TResult>(Expression<Func<T, TResult>> selector)
        {
            var fieldDefinition = new ExpressionFieldDefinition<T>(selector);
            var sort = Builders<T>.Sort.Descending(fieldDefinition);

            var entity = await _collection.Find(_filter)
                .Sort(sort)
                .Limit(1)
                .FirstOrDefaultAsync();

            return entity != null! ? selector.Compile()(entity) : default!;
        }

        public async Task<TResult> MinAsync<TResult>(Expression<Func<T, TResult>> selector)
        {
            var fieldDefinition = new ExpressionFieldDefinition<T>(selector);
            var sort = Builders<T>.Sort.Ascending(fieldDefinition);

            var entity = await _collection.Find(_filter)
                .Sort(sort)
                .Limit(1)
                .FirstOrDefaultAsync();

            return entity != null! ? selector.Compile()(entity) : default!;
        }
    }
}