using Client.Interface;

namespace Server;

/// <summary>
///     No eviction is performed.
/// </summary>
public class NullEvictionPolicy : EvictionPolicy
{
    public override EvictionType Type => EvictionType.None;


    public override string ToString()
    {
        return "NONE";
    }
}