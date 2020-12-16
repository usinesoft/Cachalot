using System;
using Client.Queries;
using NUnit.Framework;
using Tests.TestData;

namespace Tests.UnitTests
{
    [TestFixture]
    public class Subdomains
    {
        [Test]
        public void Test_subset_on_queries()
        {
            

            {
                var q1 = UtExtensions.PredicateToQuery<TradeLike>(t => t.ValueDate == DateTime.Today);
                var q2 = UtExtensions.PredicateToQuery<TradeLike>(t => t.ValueDate >= DateTime.Today.AddDays(-10));

                Assert.IsTrue(q1.IsSubsetOf(q2));
                Assert.IsFalse(q2.IsSubsetOf(q1));
            }

            {
                var q1 = UtExtensions.PredicateToQuery<TradeLike>(t => t.ValueDate == DateTime.Today && t.Folder == "EUR12");
                var q2 = UtExtensions.PredicateToQuery<TradeLike>(t => t.ValueDate >= DateTime.Today.AddDays(-10));

                Assert.IsTrue(q1.IsSubsetOf(q2));
                Assert.IsFalse(q2.IsSubsetOf(q1));
            }

            {
                var q1 = UtExtensions.PredicateToQuery<TradeLike>(t => t.Folder == "EUR12");
                var q2 = UtExtensions.PredicateToQuery<TradeLike>(t => t.Folder == "EUR12" || t.Folder == "EUR11");

                Assert.IsTrue(q1.IsSubsetOf(q2));
                Assert.IsFalse(q2.IsSubsetOf(q1));
            }

            {
                var q1 = UtExtensions.PredicateToQuery<TradeLike>(t => t.Folder == "EUR12");
                var q2 = UtExtensions.PredicateToQuery<TradeLike>(t =>
                    t.Folder == "EUR12" || t.Folder == "EUR11" && t.ValueDate == DateTime.Today);

                Assert.IsTrue(q1.IsSubsetOf(q2));
                Assert.IsFalse(q2.IsSubsetOf(q1));
            }

            {
                var q1 = UtExtensions.PredicateToQuery<TradeLike>(t => t.Folder == "EUR11");
                var q2 = UtExtensions.PredicateToQuery<TradeLike>(t =>
                    t.Folder == "EUR12" || t.Folder == "EUR11" && t.ValueDate == DateTime.Today);

                Assert.IsFalse(q1.IsSubsetOf(q2));
            }

            {
                var q1 = UtExtensions.PredicateToQuery<TradeLike>(t => t.Folder == "EUR11" && t.ValueDate == DateTime.Today);
                var q2 = UtExtensions.PredicateToQuery<TradeLike>(t =>
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