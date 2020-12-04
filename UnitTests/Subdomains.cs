using System;
using System.Resources;
using Cachalot.Linq;
using Client.Core;
using Client.Queries;
using NUnit.Framework;
using UnitTests.TestData;

namespace UnitTests
{
    [TestFixture]
    public class Subdomains
    {
        [Test]
        public void Test_subset_on_queries()
        {
            var datastore = new DataSource<TradeLike>(null, null,  ClientSideTypeDescription.RegisterType<TradeLike>().AsCollectionSchema);

            {
                var q1 = datastore.PredicateToQuery(t => t.ValueDate == DateTime.Today);
                var q2 = datastore.PredicateToQuery(t => t.ValueDate >= DateTime.Today.AddDays(-10));

                Assert.IsTrue(q1.IsSubsetOf(q2));
                Assert.IsFalse(q2.IsSubsetOf(q1));
            }

            {
                var q1 = datastore.PredicateToQuery(t => t.ValueDate == DateTime.Today && t.Folder == "EUR12");
                var q2 = datastore.PredicateToQuery(t => t.ValueDate >= DateTime.Today.AddDays(-10));

                Assert.IsTrue(q1.IsSubsetOf(q2));
                Assert.IsFalse(q2.IsSubsetOf(q1));
            }

            {
                var q1 = datastore.PredicateToQuery(t => t.Folder == "EUR12");
                var q2 = datastore.PredicateToQuery(t => t.Folder == "EUR12" || t.Folder == "EUR11");

                Assert.IsTrue(q1.IsSubsetOf(q2));
                Assert.IsFalse(q2.IsSubsetOf(q1));
            }

            {
                var q1 = datastore.PredicateToQuery(t => t.Folder == "EUR12");
                var q2 = datastore.PredicateToQuery(t =>
                    t.Folder == "EUR12" || t.Folder == "EUR11" && t.ValueDate == DateTime.Today);

                Assert.IsTrue(q1.IsSubsetOf(q2));
                Assert.IsFalse(q2.IsSubsetOf(q1));
            }

            {
                var q1 = datastore.PredicateToQuery(t => t.Folder == "EUR11");
                var q2 = datastore.PredicateToQuery(t =>
                    t.Folder == "EUR12" || t.Folder == "EUR11" && t.ValueDate == DateTime.Today);

                Assert.IsFalse(q1.IsSubsetOf(q2));
            }

            {
                var q1 = datastore.PredicateToQuery(t => t.Folder == "EUR11" && t.ValueDate == DateTime.Today);
                var q2 = datastore.PredicateToQuery(t =>
                    t.Folder == "EUR12" || t.Folder == "EUR11" && t.ValueDate > DateTime.Today.AddDays(-10));

                Assert.IsTrue(q1.IsSubsetOf(q2));
                Assert.IsFalse(q2.IsSubsetOf(q1));

                // every query is a subset of an empty query
                Assert.IsTrue(q1.IsSubsetOf(OrQuery.Empty<TradeLike>()));
                Assert.IsTrue(q2.IsSubsetOf(OrQuery.Empty<TradeLike>()));
            }
        }
    }
}