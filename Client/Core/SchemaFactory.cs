using System;
using System.Collections.Generic;
using System.Linq;
using Client.Messages;
using JetBrains.Annotations;

namespace Client.Core
{
    /// <summary>
    ///     Programatically create type description using fluent syntax
    /// </summary>
    public static class SchemaFactory
    {
        public class FluentToken
        {
            private CollectionSchema Product { get; }

            internal FluentToken(CollectionSchema schema)
            {
                Product = schema;
            }

            private int _index;
            private int _collectionIndex;


            public FluentToken PrimaryKey(string name)
            {
                if (Product.ServerSide.Count != 0)
                    throw new NotSupportedException("A primary key had already been defined for this collection");

                Product.ServerSide.Add(new KeyInfo(name, 0, IndexType.Primary));

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

                Product.ServerSide.Add(new KeyInfo(name, order, indexType));


                return this;
            }


            public FluentToken WithServerSideCollection(string name)
            {
                if (Product.PrimaryKeyField == null)
                    throw new NotSupportedException("A primary key has to be defined first");

                var order = _collectionIndex++;

                Product.ServerSide.Add(new KeyInfo(name, order, IndexType.Dictionary, null,
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



        public static FluentToken New([NotNull] string collectionName, Layout storageLayout = Layout.Default)
        {
            if (collectionName == null) throw new ArgumentNullException(nameof(collectionName));


            return new FluentToken(new CollectionSchema
            {
                CollectionName = collectionName,
                StorageLayout = storageLayout,
            });
        }
    }
}