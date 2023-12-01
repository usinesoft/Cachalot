using Client.Core;

namespace Client.Queries;

public sealed class EmptyQuery : Query
{
    public override bool IsValid => true;
    public override bool IsEmpty()
    {
        return true;
    }

    public override bool Match(PackedObject item)
    {
        return true;
    }
}