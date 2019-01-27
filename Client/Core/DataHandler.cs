namespace Client.Core
{
    /// <summary>
    ///     A handler called each time an object is received
    /// </summary>
    /// <typeparam name="TItemType">concrete item type</typeparam>
    /// <param name="item">received object</param>
    /// <param name="currentItem">index of the current object </param>
    /// <param name="totalItems">number of objects expected</param>
    public delegate void DataHandler<in TItemType>(TItemType item, int currentItem, int totalItems);
}