namespace Client.Queries;

public static class OperatorExtensions
{
    public static bool IsRangeOperator(this QueryOperator @this)
    {
        if (@this == QueryOperator.GeLe || @this == QueryOperator.GeLt || @this == QueryOperator.GtLt ||
            @this == QueryOperator.GtLe)
            return true;


        return false;
    }
}