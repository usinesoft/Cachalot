# Cachalot

## What is new in this version

### Last One
- No need any more to specify the data type for indexes.
- Bug correction; AVoid client messages to be processed in some cases before the server has fully charged persistent data

### Previous
It improves the full-text search: faster and more pertinent.
All the executables target the platform netcore 3.1. The Nugget package containing the client code is netstandard 2.1. It can be used both in core and classical applications.
The admin console is now supported on Linux too. The Windows service was ported to netcore 3.1 (it will only run on Windows as a service).
We added a new eviction policy (in pure cache mode): TTL (Time To Live). It automatically removes those that are older than a given age (specified for each type of data).
A simple connection string can be used to connect to a cluster instead of the configuration file.


## What is Cachalot DB?

Cachalot DB is more than one solution. 
 
It is a distributed cache with unique features. It can do the usual things distributed caches do. Retrieve items by one or more unique keys and remove them according to different eviction policies. But it can do much more. Using a smart LINQ extension, you can run SQL-like queries, and they return a result set if and only if the whole subset of data is available in the cache. 
 
Cachalot is also a fully-featured in-memory database. Like REDIS but with a full query model, not only a key-values dictionary.  And unlike REDIS, it is entirely transactional. 

A powerful LINQ provider is available, as well as an administration console.
Much more detail in the next sections, but


## Show me some code first

Let’s prepare our business objects for database storage.
We start with a toy web site which allows to rent homes between individuals.
A simple description of a real estate property would be. 

```
public class Home
{
	public string CountryCode { get; set; }
	public string Town { get; set; }
	public string Adress { get; set; }
	public string Owner { get; set; }
	public string OwnerEmail { get; set; }
	public string OwnerPhone { get; set; }
	public int Rooms { get; set; }
	public int Bathrooms { get; set; }
	public int PriceInEuros { get; set; }
}
```


The first requirement for a business object is to have a primary key. As there is no “natural” one in this case, we will add a numeric Id.

```
public class Home
{
	[PrimaryKey(KeyDataType.IntKey)]
	public int Id { get; set; }

	°°°
}
```

Now the object can be stored in the database.
First step is to instantiate a **Connector** which needs a "client configuration". More on the configuration later but, for now, it needs to contain the list of servers in the cluster. To start, only one run locally.
We can read the configuration from an external file or specify it as a connection string. For the moment, let’s build it manually.

```
var config = new ClientConfig
{
	Servers = {new ServerConfig {Host = "localhost", Port = 4848}}
};

using (var connector = new Cachalot.Linq.Connector(config))
{
	var homes = connector.DataSource<Home>();
	// the rest of the code goes here
}
```


There is one last step before storing an object in the database. We need to generate a unique value for the primary key. We can produce multiple unique values with a single call.
Unlike other databases, you do not need to create a unique value generator explicitly. The first call with an unknown generator name will automatically create it.


```
var ids = connector.GenerateUniqueIds("home_id", 1);

var home = new Home
{
	Id = ids[0],
	Adress = "14 rue du chien qui fume",
	Bathrooms = 1,
	CountryCode = "FR",
	PriceInEuros = 125,
	Rooms = 2, 
	Town = "Paris"
};

homes.Put(home);
```

Now your first object is safely stored in the database.
For the moment, you can only retrieve it by primary key. That can be done in two equivalent ways.

```
var reloaded = homes[property.Id];
```

Or with a LINQ expression.

```
var reloaded = homes.First(p => p.Id == property.Id);
```

The first one is faster as there is no need to parse the expression tree. 
In most relational databases, we use two distinct operations: INSERT and UPDATE. In Cachalot Db only one operation is exposed: PUT 
It will insert new items (new primary key) and will update the existing ones.

You probably have higher expectations from a modern database than merely storing and retrieving objects by primary key. And you are right.


The whole user guide, including an administration section is available here

https://github.com/usinesoft/Cachalot/blob/master/Doc/CachalotUserGuide.pdf





