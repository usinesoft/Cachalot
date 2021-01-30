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

            private int NewIndex()
            {
                _index++;
                return _index;
            }

            public FluentToken PrimaryKey(string name)
            {
                if (Product.ServerSide.Count != 0)
                    throw new NotSupportedException("A primary key had already been defined for this collection");

                Product.ServerSide.Add(new KeyInfo(name, 0, IndexType.Primary));

                return this;
            }

            public FluentToken WithServerSideValue(string name,
                IndexType indexType = IndexType.None)
            {
                if (Product.PrimaryKeyField == null)
                    throw new NotSupportedException("A primary key has to be defined first");

                var order = NewIndex();

                Product.ServerSide.Add(new KeyInfo(name, order, indexType));


                return this;
            }


            public FluentToken WithServerSideCollection(string name)
            {
                if (Product.PrimaryKeyField == null)
                    throw new NotSupportedException("A primary key has to be defined first");

                var order = NewIndex();

                Product.ServerSide.Add(new KeyInfo(name, order + CollectionOrderOffset, IndexType.Dictionary, null,
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

                Product.ServerSide = Product.ServerSide.OrderBy(k => k.Order).ToList();

                // Adjust order. Line numbers give relative order of keys but they need to be adjusted to a continuous range with the primary key at index 0 and the collections at the end
                var lineNumbers = new HashSet<int>();
                foreach (var keyInfo in Product.ServerSide)
                {
                    lineNumbers.Add(keyInfo.Order);
                }

            
                var lineNumberByPosition = lineNumbers.OrderBy(x => x).ToList();

                foreach (var keyInfo in Product.ServerSide)
                {
                    var adjustedOrder = lineNumberByPosition.FindIndex(l => l == keyInfo.Order);
                    keyInfo.Order = adjustedOrder;
                }


                Product.ServerSide = Product.ServerSide.OrderBy(k => k.Order).ToList();


                return Product;
            }
        }


        private const int CollectionOrderOffset = 1000;

        public static FluentToken New([NotNull] string collectionName, bool useCompression = false)
        {
            if (collectionName == null) throw new ArgumentNullException(nameof(collectionName));

            var name = collectionName.Split('.').Last();

            return new FluentToken(new CollectionSchema
            {
                CollectionName = collectionName,
                UseCompression = useCompression,
                TypeName = name
            });
        }
    }
}