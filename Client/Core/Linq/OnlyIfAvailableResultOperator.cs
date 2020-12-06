using System;
using System.Linq.Expressions;
using Remotion.Linq.Clauses;
using Remotion.Linq.Clauses.ResultOperators;
using Remotion.Linq.Clauses.StreamedData;

namespace Client.Core.Linq
{
    public class OnlyIfAvailableResultOperator
        : SequenceTypePreservingResultOperatorBase

    {
        public OnlyIfAvailableResultOperator(Expression parameter)

        {
            Parameter = parameter;
        }


        public Expression Parameter { get; private set; }


        public override string ToString()

        {
            return "Only if available";
        }


        public override ResultOperatorBase Clone(CloneContext cloneContext)

        {
            return new OnlyIfAvailableResultOperator(Parameter);
        }


        public override void TransformExpressions(
            Func<Expression, Expression> transformation)

        {
            Parameter = transformation(Parameter);
        }


        public override StreamedSequence ExecuteInMemory<T>(StreamedSequence input)

        {
            return input; // sequence is not changed by this operator
        }
    }
}