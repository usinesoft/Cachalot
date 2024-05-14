using Client.Core;
using Client.Interface;

namespace DatasetGenerator.DataModel;



public class Product
{
    [ServerSideValue(IndexType.Primary)]
    public Guid Key { get; set; } = Guid.NewGuid();

    [ServerSideValue(IndexType.Dictionary)]
    public string Category { get; set; }
    
    [ServerSideValue(IndexType.Dictionary)]
    public string Subcategory { get; set; }
    
    [FullTextIndexation]
    [ServerSideValue(IndexType.None)]
    public string Name { get; set; }
    
    [ServerSideValue(IndexType.Ordered)]
    public double CurrentPrice { get; set; }
    public double RawPrice { get; set; }
    public string Currency { get; set; }
    
    [ServerSideValue(IndexType.Ordered)]
    public int Discount { get; set; }

    [ServerSideValue(IndexType.Ordered)]
    public int LikesCount { get; set; }
    public bool IsNew { get; set; }
    
    [FullTextIndexation]
    [ServerSideValue(IndexType.Dictionary)]
    public string Brand { get; set; }
    public string BrandUrl { get; set; }
    public string CodCountry { get; set; }
    [FullTextIndexation]
    public string Variation0Color { get; set; }
    [FullTextIndexation]
    public string Variation1Color { get; set; }
    public string Variation0Thumbnail { get; set; }
    public string Variation0Image { get; set; }
    public string Variation1Thumbnail { get; set; }
    public string Variation1Image { get; set; }
    public string ImageUrl { get; set; }
    public string Url { get; set; }
    
    [ServerSideValue(IndexType.Dictionary)]
    public int Id { get; set; }
    
    [ServerSideValue(IndexType.Dictionary)]
    public string Model { get; set; }
}