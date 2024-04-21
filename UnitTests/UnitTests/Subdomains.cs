using System;
using Client.Queries;
using NUnit.Framework;
using NUnit.Framework.Legacy;
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

                ClassicAssert.IsTrue(q1.IsSubsetOf(q2));
                ClassicAssert.IsFalse(q2.IsSubsetOf(q1));
            }

            {
                var q1 = UtExtensions.PredicateToQuery<TradeLike>(t =>
                    t.ValueDate == DateTime.Today && t.Folder == "EUR12");
                var q2 = UtExtensions.PredicateToQuery<TradeLike>(t => t.ValueDate >= DateTime.Today.AddDays(-10));

                ClassicAssert.IsTrue(q1.IsSubsetOf(q2));
                ClassicAssert.IsFalse(q2.IsSubsetOf(q1));
            }

            {
                var q1 = UtExtensions.PredicateToQuery<TradeLike>(t => t.Folder == "EUR12");
                var q2 = UtExtensions.PredicateToQuery<TradeLike>(t => t.Folder == "EUR12" || t.Folder == "EUR11");

                ClassicAssert.IsTrue(q1.IsSubsetOf(q2));
                ClassicAssert.IsFalse(q2.IsSubsetOf(q1));
            }

            {
                var q1 = UtExtensions.PredicateToQuery<TradeLike>(t => t.Folder == "EUR12");
                var q2 = UtExtensions.PredicateToQuery<TradeLike>(t =>
                    t.Folder == "EUR12" || (t.Folder == "EUR11" && t.ValueDate == DateTime.Today));

                ClassicAssert.IsTrue(q1.IsSubsetOf(q2));
                ClassicAssert.IsFalse(q2.IsSubsetOf(q1));
            }

            {
                var q1 = UtExtensions.PredicateToQuery<TradeLike>(t => t.Folder == "EUR11");
                var q2 = UtExtensions.PredicateToQuery<TradeLike>(t =>
                    t.Folder == "EUR12" || (t.Folder == "EUR11" && t.ValueDate == DateTime.Today));

                ClassicAssert.IsFalse(q1.IsSubsetOf(q2));
            }

            {
                var q1 = UtExtensions.PredicateToQuery<TradeLike>(t =>
                    t.Folder == "EUR11" && t.ValueDate == DateTime.Today);
                var q2 = UtExtensions.PredicateToQuery<TradeLike>(t =>
                    t.Folder == "EUR12" || (t.Folder == "EUR11" && t.ValueDate > DateTime.Today.AddDays(-10)));

                ClassicAssert.IsTrue(q1.IsSubsetOf(q2));
                ClassicAssert.IsFalse(q2.IsSubsetOf(q1));

                // every query is a subset of an empty query
                ClassicAssert.IsTrue(q1.IsSubsetOf(OrQuery.Empty<TradeLike>()));
                ClassicAssert.IsTrue(q2.IsSubsetOf(OrQuery.Empty<TradeLike>()));
            }
        }
    }
}