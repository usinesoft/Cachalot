# Cachalot
What is Cachalot DB?

A very fast, open source, NO SQL, fully transactional database for .NET applications.
It is distributed, it scales linearly with the number of nodes. On a single node you can durably insert fifty thousand objects per second on a modest system.  
A powerful LINQ provider is available. As well as an administration console.
It can also be used as an abvanced, transactional, distributed cache with unique features.
Much more detail in the next sections but…

##Show me some code first

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


