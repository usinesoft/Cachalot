using Client.Core;

namespace Client.Queries;

public class EmptyQuery : Query
{
    public sealed override bool IsValid => true;

    public sealed override bool Match(PackedObject item)
    {
        return true;
    }
}