using System;
using System.ComponentModel;
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
            public CollectionSchema Product { get; }

            public FluentToken(CollectionSchema schema)
            {
                Product = schema;
            }

            private int _index;

            public int NewIndex()
            {
                _index++;
                return _index;
            }
        }


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

        public static FluentToken PrimaryKey(this FluentToken @this, string name)
        {
            if (@this.Product.ServerSideValues.Count != 0)
                throw new NotSupportedException("A primary key had already been defined for this collection");

            @this.Product.ServerSide.Add(new KeyInfo(name, 0, IndexType.Primary));

            return @this;
        }


        public static FluentToken WithServerSideValue(this FluentToken @this, string name,
            IndexType indexType = IndexType.None)
        {
            if (@this.Product.PrimaryKeyField == null)
                throw new NotSupportedException("A primary key has to be defined first");

            var order = @this.NewIndex();

            @this.Product.ServerSide.Add(new KeyInfo(name, order, indexType));


            return @this;
        }

        public static FluentToken EnableFullTextSearch(this FluentToken @this, params string[] propertyNames)
        {
            if(propertyNames.Length == 0)
                throw new NotSupportedException("A list of properties must be specified to enable full-text search");

            foreach (var propertyName in propertyNames)
            {
                @this.Product.FullText.Add(propertyName);
            }
            

            return @this;
        }

        public static CollectionSchema Build(this FluentToken @this)
        {
            if (@this.Product.PrimaryKeyField == null)
                throw new NotSupportedException("The primary key is mandatory when defining a collection schema");

            return @this.Product;
        }
    }
}