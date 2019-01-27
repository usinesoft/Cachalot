using System;
using Client.Core;
using Client.Queries;
using NUnit.Framework;
using UnitTests.TestData;

namespace UnitTests
{
    [TestFixture]
    public class TestFixtureDomains
    {
        [Test]
        public void AndQueries()
        {
            QueryBuilder builder = new QueryBuilder(typeof(TradeLike));

            DomainDescription domainAll = new DomainDescription(typeof(TradeLike));
            domainAll.IsFullyLoaded = true;

            //check the trivial case of a complete domain (all queries are subsets of a complete domain)
            AndQuery q1 = builder.MakeAndQuery(builder.MakeAtomicQuery("Folder", "BBB"));
            Assert.IsTrue(q1.IsSubsetOf(domainAll));

            //create a complex domain definition containing all data for Folder == AAA and for ValueDate > 20010101
            DomainDescription complexDomain = new DomainDescription(typeof(TradeLike));
            AtomicQuery q2 = builder.MakeAtomicQuery("Folder", "AAA");
            AtomicQuery q3 = builder.MakeAtomicQuery("Folder", "BBB");
            AtomicQuery q4 = builder.MakeAtomicQuery("ValueDate", QueryOperator.Gt, new DateTime(2001, 1, 1));
            complexDomain.AddOrReplace(q2);
            complexDomain.AddOrReplace(q4);

            AndQuery q23 = builder.MakeAndQuery();
            q23.Elements.Add(q2);
            q23.Elements.Add(q3);

            Assert.IsTrue(q23.IsSubsetOf(complexDomain));

            AtomicQuery q5 = builder.MakeAtomicQuery("ValueDate", QueryOperator.Ge, new DateTime(2001, 1, 1));
            AndQuery q55 = builder.MakeAndQuery(q5);

            Assert.IsFalse(q55.IsSubsetOf(complexDomain));
        }

        [Test]
        public void AtomicQueries()
        {
            QueryBuilder builder = new QueryBuilder(typeof(TradeLike));

            DomainDescription domainAll = new DomainDescription(typeof(TradeLike));
            domainAll.IsFullyLoaded = true;

            //check the trivial case of a complete domain (all queries are subsets of a complete domain)
            AtomicQuery q1 = builder.MakeAtomicQuery("ValueDate", QueryOperator.Ge, DateTime.Now);
            Assert.IsTrue(q1.IsSubsetOf(domainAll));


            DomainDescription domainFolderAAA = new DomainDescription(typeof(TradeLike));
            AtomicQuery q2 = builder.MakeAtomicQuery("Folder", "AAA");
            AtomicQuery q3 = builder.MakeAtomicQuery("Folder", "BBB");

            domainFolderAAA.AddOrReplace(q2);
            Assert.IsTrue(q2.IsSubsetOf(domainFolderAAA));
            Assert.IsFalse(q3.IsSubsetOf(domainFolderAAA));

            domainFolderAAA.AddOrReplace(q3);

            Assert.IsTrue(q2.IsSubsetOf(domainFolderAAA));
            Assert.IsTrue(q3.IsSubsetOf(domainFolderAAA));

            //create a complex domain definition containg all data for Folder == AAA and for ValueDate > 20010101
            DomainDescription complexDomain = new DomainDescription(typeof(TradeLike));
            AtomicQuery q4 = builder.MakeAtomicQuery("ValueDate", QueryOperator.Ge, new DateTime(2001, 1, 1));
            complexDomain.AddOrReplace(q2);
            complexDomain.AddOrReplace(q4);

            Assert.IsTrue(q2.IsSubsetOf(complexDomain));
            Assert.IsTrue(q4.IsSubsetOf(complexDomain));

            AtomicQuery q5 = builder.MakeAtomicQuery("ValueDate", new DateTime(2001, 1, 1));
            Assert.IsTrue(q5.IsSubsetOf(complexDomain));
            AtomicQuery q6 = builder.MakeAtomicQuery("ValueDate", QueryOperator.Gt, new DateTime(2001, 1, 1));
            Assert.IsTrue(q6.IsSubsetOf(complexDomain));

            AtomicQuery q7 = builder.MakeAtomicQuery("ValueDate", QueryOperator.Gt, new DateTime(2001, 1, 2));
            Assert.IsTrue(q7.IsSubsetOf(complexDomain));

            AtomicQuery q8 = builder.MakeAtomicQuery("ValueDate", QueryOperator.Lt, new DateTime(2001, 1, 2));
            Assert.IsFalse(q8.IsSubsetOf(complexDomain));
        }

        [Test]
        public void OrQueries()
        {
            QueryBuilder builder = new QueryBuilder(typeof(TradeLike));

            DomainDescription domainAll = new DomainDescription(typeof(TradeLike));
            domainAll.IsFullyLoaded = true;

            OrQuery q = builder.GetManyWhere("VALUEDATE > 20100101");
            Assert.IsTrue(q.IsSubsetOf(domainAll));

            DomainDescription complexDomain = new DomainDescription(typeof(TradeLike));
            AtomicQuery q2 = builder.MakeAtomicQuery("Folder", "AAA");
            AtomicQuery q4 = builder.MakeAtomicQuery("ValueDate", QueryOperator.Gt, new DateTime(2001, 1, 1));
            complexDomain.AddOrReplace(q2);
            complexDomain.AddOrReplace(q4);
            OrQuery qq = builder.GetManyWhere("FOLDER = AAA, VALUEDATE > 20010101");
            Assert.IsTrue(qq.IsSubsetOf(complexDomain));

            OrQuery qqq = builder.GetManyWhere("VALUEDATE >= 20010101");
            Assert.IsFalse(qqq.IsSubsetOf(complexDomain));

            AtomicQuery q3 = builder.MakeAtomicQuery("Folder", "BBB");
            AndQuery q33 = builder.MakeAndQuery(q3);
            qq.Elements.Add(q33);
            //the query is now (FOLDER = AAA AND VALUEDATE > 20010101) OR (FOLDER = BBB ) ans is not a subset
            //any more
            Assert.IsFalse(qq.IsSubsetOf(complexDomain));
        }
    }
}