# Cachalot

## What is Cachalot DB?

A very fast, open source, NO SQL, fully transactional database for .NET applications.
It is distributed, it scales linearly with the number of nodes. On a single node you can durably insert fifty thousand objects per second on a modest system.  
A powerful LINQ provider is available. As well as an administration console.
It can also be used as an abvanced, transactional, distributed cache with unique features.
Much more detail in the next sections but…

##S how me some code first

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
The configuration is usually read from an external file. For the moment, let’s build it manually

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


One last step before storing an object in the database. We need to generate a unique value for the primary key. Multiple unique values can be generated with a single call.
Unlike other databases, you do not need to explicitly create a unique value generator. First call with an unknown generator name will automatically create it.

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
In most relational databases we use two distinct operations: INSERT and UPDATE. In Cachalot Db only one operation is exposed: PUT 
It will insert new items (new primary key) and will update existing items.

You probably have higher expectation from a modern database than simply storing and retrieving objects by primary key. And you are right.

The whole user guide, including an administration section is available here

https://github.com/usinesoft/Cachalot/blob/master/Doc/CachalotUserGuide.pdf





