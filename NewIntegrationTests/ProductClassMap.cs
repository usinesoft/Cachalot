using CsvHelper.Configuration;

namespace DatasetGenerator.DataModel;

public sealed class ProductClassMap : ClassMap<Product>
{
    public ProductClassMap()
    {
        Map(m => m.Category).Name("category");
        Map(m => m.Subcategory).Name("subcategory");
        Map(m => m.Name).Name("name");
        Map(m => m.CurrentPrice).Name("current_price");
        Map(m => m.RawPrice).Name("raw_price");
        Map(m => m.Currency).Name("currency");
        Map(m => m.Discount).Name("discount");
        Map(m => m.LikesCount).Name("likes_count");
        Map(m => m.IsNew).Name("is_new");
        Map(m => m.Brand).Name("brand");
        Map(m => m.BrandUrl).Name("brand_url");
        Map(m => m.CodCountry).Name("codCountry");
        Map(m => m.Variation0Color).Name("variation_0_color");
        Map(m => m.Variation1Color).Name("variation_1_color");
        Map(m => m.Variation0Thumbnail).Name("variation_0_thumbnail");
        Map(m => m.Variation0Image).Name("variation_0_image");
        Map(m => m.Variation1Thumbnail).Name("variation_1_thumbnail");
        Map(m => m.Variation1Image).Name("variation_1_image");
        Map(m => m.ImageUrl).Name("image_url");
        Map(m => m.Url).Name("url");
        Map(m => m.Id).Name("id");
        Map(m => m.Model).Name("model");
    }
}