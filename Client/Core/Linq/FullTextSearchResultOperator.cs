using System;
using System.Linq.Expressions;
using Remotion.Linq.Clauses;
using Remotion.Linq.Clauses.ResultOperators;
using Remotion.Linq.Clauses.StreamedData;

namespace Client.Core.Linq
{
    public class FullTextSearchResultOperator
        : SequenceTypePreservingResultOperatorBase

    {
        public FullTextSearchResultOperator(Expression parameter)

        {
            Parameter = parameter;
        }


        public Expression Parameter { get; private set; }


        public override string ToString()

        {
            return "Full text search";
        }


        public override ResultOperatorBase Clone(CloneContext cloneContext)

        {
            return new FullTextSearchResultOperator(Parameter);
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