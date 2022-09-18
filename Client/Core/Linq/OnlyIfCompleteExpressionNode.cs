using Remotion.Linq.Clauses;
using Remotion.Linq.Parsing.Structure.IntermediateModel;
using System.Linq.Expressions;
using System.Reflection;

namespace Client.Core.Linq
{
    public class OnlyIfCompleteExpressionNode : ResultOperatorExpressionNodeBase

    {
        public static readonly MethodInfo[] SupportedMethods =
            {typeof(MyQueryExtensions).GetMethod("OnlyIfComplete")};


        private readonly Expression _parameterLambda;


        public OnlyIfCompleteExpressionNode(
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

            return new OnlyIfAvailableResultOperator(_parameterLambda);
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
}