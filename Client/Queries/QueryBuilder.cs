#region

using Client.Core;
using Client.Parsing;
using System;

#endregion

namespace Client.Queries
{
    /// <summary>
    ///     Helper class (produce queries for the most common cases)
    /// </summary>
    public class QueryBuilder
    {
        private readonly CollectionSchema _collectionSchema;

        /// <summary>
        ///     Initialize for a specified cacheable data type
        /// </summary>
        /// <param name="type"></param>
        public QueryBuilder(Type type)
        {
            _collectionSchema = TypedSchemaFactory.FromType(type);
        }

        /// <summary>
        ///     Create from a <see cref="CollectionSchema" />
        /// </summary>
        /// <param name="collectionSchema"></param>
        public QueryBuilder(CollectionSchema collectionSchema)
        {
            _collectionSchema = collectionSchema ?? throw new ArgumentNullException(nameof(collectionSchema));
        }

        /// <summary>
        ///     Get one by primary key
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public OrQuery GetOne(object value)
        {
            var keyValue = value as KeyValue ?? new KeyValue(value);

            var query = new OrQuery(_collectionSchema.CollectionName);
            var andQuery = new AndQuery();
            query.Elements.Add(andQuery);
            andQuery.Elements.Add(new AtomicQuery(_collectionSchema.PrimaryKeyField, keyValue));
            query.ByPrimaryKey = true;

            return query;
        }



        /// <summary>
        /// Create a query from SQL
        /// </summary>
        /// <param name="sql"></param>
        /// <returns></returns>
        public OrQuery FromSql(string sql)
        {
            return new Parser().ParseSql(sql).ToQuery(_collectionSchema);
        }


    }
}