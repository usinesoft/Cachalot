using System.Linq.Expressions;
using System.Reflection;
using Remotion.Linq.Clauses;
using Remotion.Linq.Parsing.Structure.IntermediateModel;

namespace Client.Core.Linq;

public class FullTextSearchExpressionNode : ResultOperatorExpressionNodeBase

{
    public static readonly MethodInfo[] SupportedMethods =
        { typeof(MyQueryExtensions).GetMethod("FullTextSearch") };


    private readonly Expression _parameterLambda;


    public FullTextSearchExpressionNode(
        MethodCallExpressionParseInfo parseInfo, Expression parameter)
        : base(parseInfo, null, null)

    {
        _parameterLambda = parameter;
    }


    protected override ResultOperatorBase CreateResultOperator(
        ClauseGenerationContext clauseGenerationContext)

    {
        //var resolvedParameter = Source.Resolve(
        //    _parameterLambda.Parameters[0],
        //    _parameterLambda.Body,
        //    clauseGenerationContext);

        return new FullTextSearchResultOperator(_parameterLambda);
    }


    public override Expression Resolve(
        ParameterExpression inputParameter,
        Expression expressionToBeResolved,
        ClauseGenerationContext clauseGenerationContext)

    {
        return Source.Resolve(
            inputParameter,
            expressionToBeResolved,
            clauseGenerationContext);
    }
}