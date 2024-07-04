
## What is Cachalot DB?
***An open-source (MIT license), in-memory database for dotnet applications***   

All data is available in memory and distributed on multiple nodes, allowing for blazing-fast queries. Like Redis but with some significant differences:

* 	**Persistence is transactional**; all update operations are written into a persistent transaction log before being applied to memory.
* 	It has a **full query model** available through **SQL** and **LINQ**. It supports all usual operators like projections, distinct, order by, and, of course, a complete where clause. Everything is done server-side.
* 	It supports both dictionary and ordered indexes. A highly optimized query processor chooses the best index, combining multiple indexes for one query.
* 	It can compute **pivot tables** server-side even on a cluster of many nodes.
* 	A high-speed **full-text search**. You can do either full-text search, traditional queries, or combine the two.
* 	Very fast, fully ACID, **two-stage transactions** on multiple nodes
* 	**Consistent read context** allows executing a sequence of queries in a context that guarantees that data is not modified meanwhile.
* 	**Bulk inserts**: when feeding with large quantities of data, ordered indexes are sorted only once at the end to ensure optimum performance.
* 	When used as a distributed cache (persistence disabled), an inventive mechanism allows the description of the cached data, thus enabling complex queries to be served from cache only if all the concerned data is available.

## How fast is it?
***Very fast***   

A live demo is available [here](https://demo.cachalotdb.com).

Two demo applications are available in the release package:

-	**BookingMarketplace** is testing feeding data and query capabilities
-	**Accounts** is testing the transactional capabilities

A bencnhmark is also included in the solution.
Here are the results on Windows with a local single-node cluster. 

Results are **microseconds**.


| Objects returned | Query                                                                                                  | Mean       | Error    | StdDev    |
|-----|---------------------------------------------------------------------------------------------------------------------|-----------:|---------:|----------:|
| 1   |  SELECT FROM Invoice WHERE Id = '50011'                                                                             |   133.9 us |  2.67 us |   2.50 us |
| 27  | SELECT FROM Invoice WHERE Date = '2023-04-23' AND DiscountPercentage > 0                                            |   808.4 us | 12.69 us |  11.87 us |
| 57  | SELECT FROM Invoice WHERE Date in ('2023-04-22','2023-04-23') AND  IsPayed = false                                  | 1,499.2 us | 25.61 us |  22.70 us |
| 86  | SELECT FROM Client WHERE LastName = 'Corwin'                                                                        |   774.8 us | 14.06 us |  13.15 us |
| 314 | SELECT FROM Home WHERE Town = 'Paris' AND  AvailableDates contains '2024-05-05' AND Rooms = 2 ORDER BY PriceInEuros | 3,910.1 us | 78.09 us | 190.08 us |
| |SELECT DISTINCT Town from Home                                                                                           |   158.0 us |  0.45 us |   0.40 us |


## What is Cachalot DB good at?
***We designed Cachalot DB to be blazing fast and transactional.***   



As always, there is a trade-off in terms of what it can not do.

> The infamous CAP Theorem proves that a distributed system cannot be at
> the same time fault-tolerant and transactionally consistent, and we
> chose transactional consistency.

To achieve high-speed data access, it loads all data in memory.

It means you need enough memory to store all your data. 

Each node loads everything in memory when it starts.
>Cachalot is a contraction of "Cache a lot" 😊

We have tested up to 200 GB of data and one hundred million medium-sized objects per collection. It can scale even more, but it is probably not the right technology to choose if you need to store more than 1 TB of data.

We can use it as a very efficient cache for big-data applications but not the golden source.


