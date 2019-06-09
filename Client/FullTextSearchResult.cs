namespace Client
{

    /// <summary>
    /// Wrapper used to add full-text search score to a found item
    /// </summary>
    public class FullTextSearchResult<TItem>
    {
        public double Score { get; set; }

        public TItem Item { get; set; }
    }
}
