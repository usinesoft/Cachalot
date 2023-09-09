using System;
using JetBrains.Annotations;

namespace Client.Core;

/// <summary>
///     Programatically create type description using fluent syntax
/// </summary>
public static class SchemaFactory
{
    public static FluentToken New([NotNull] string collectionName, Layout storageLayout = Layout.Default)
    {
        if (collectionName == null) throw new ArgumentNullException(nameof(collectionName));


        return new(new()
        {
            CollectionName = collectionName,
            StorageLayout = storageLayout
        });
    }

    public class FluentToken
    {
        private int _collectionIndex;

        private int _index;

        internal FluentToken(CollectionSchema schema)
        {
            Product = schema;
        }

        private CollectionSchema Product { get; }


        public FluentToken PrimaryKey(string name)
        {
            if (Product.ServerSide.Count != 0)
                throw new NotSupportedException("A primary key had already been defined for this collection");

            Product.ServerSide.Add(new(name, 0, IndexType.Primary));

            _index = 1; // 0 is reserved for the primary key
            _collectionIndex = 0;

            return this;
        }

        public FluentToken WithServerSideValue(string name,
                                               IndexType indexType = IndexType.None)
        {
            if (Product.PrimaryKeyField == null)
                throw new NotSupportedException("A primary key has to be defined first");

            var order = _index++;

            Product.ServerSide.Add(new(name, order, indexType));


            return this;
        }


        public FluentToken WithServerSideCollection(string name)
        {
            if (Product.PrimaryKeyField == null)
                throw new NotSupportedException("A primary key has to be defined first");

            var order = _collectionIndex++;

            Product.ServerSide.Add(new(name, order, IndexType.Dictionary, null,
                true));


            return this;
        }

        public FluentToken EnableFullTextSearch(params string[] propertyNames)
        {
            if (propertyNames.Length == 0)
                throw new NotSupportedException(
                    "A list of properties must be specified to enable full-text search");

            foreach (var propertyName in propertyNames) Product.FullText.Add(propertyName);


            return this;
        }

        public CollectionSchema Build()
        {
            if (Product.PrimaryKeyField == null)
                throw new NotSupportedException("The primary key is mandatory when defining a collection schema");


            return Product;
        }
    }
}