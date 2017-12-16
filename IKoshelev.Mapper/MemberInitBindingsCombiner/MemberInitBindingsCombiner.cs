using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Linq;
using System.Text;

namespace IKoshelev.Mapper.MemberInitBindingsCombiner
{
    public class MemberInitBindingsCombiner<TSource, TDestination>
    {
        public Expression<Func<TSource, TDestination>> CombineIntoMapperWithConstructor(         
            Expression<Func<TSource, TDestination>> recipientExpression,
            Expression<Func<TSource, TDestination>> donorExpression)
        {
            var visitor = new MemberInitBindingsCombinationVisitor<TSource, TDestination>()
            {               
                recipientExpression = recipientExpression,
                donorExpression = donorExpression
            };

            var combinedExpr = (Expression<Func<TSource, TDestination>>) visitor.Visit(recipientExpression);

            return combinedExpr;
        }

        public Expression<Action<TSource, TDestination>> CombineIntoMapperForExisting(
            Expression<Func<TSource, TDestination>> expressionA,
            Expression<Func<TSource, TDestination>> expressionB)
        {
            var bindingsA = MemberInitBindingsCombinationVisitor<TSource, TDestination>.GetBindings(expressionA);
            var bindingsB = MemberInitBindingsCombinationVisitor<TSource, TDestination>.GetBindings(expressionB);

            var paramDestination = Expression.Parameter(typeof(TDestination), "destination");
            var paramSource = Expression.Parameter(typeof(TSource), "source");

            BinaryExpression GetAssignment(ParameterExpression paramSourceExisting, MemberBinding binding)
            {
                var assignment = MemberInitBindingsCombinationVisitor<TSource, TDestination>
                                           .GetAssignmentFromBinding(binding,
                                                                       paramSourceExisting,
                                                                       paramSource,
                                                                       paramDestination);
                return assignment;
            }

            var assignmentsA = bindingsA
                                    .Select(binding => 
                                                GetAssignment(expressionA.Parameters[0], binding));

            var assignmentsB = bindingsB
                                    .Select(binding => 
                                                GetAssignment(expressionB.Parameters[0], binding));

            var combinedAssignments = assignmentsA
                                            .Union(assignmentsB)
                                            .ToArray();

            var body = Expression.Block(combinedAssignments);

            return Expression.Lambda<Action<TSource, TDestination>>(body, new[] { paramSource, paramDestination });
        }     
    }

    internal class MemberInitBindingsCombinationVisitor<TSource, TDestination> : ExpressionVisitor
    {       
        internal Expression<Func<TSource, TDestination>> recipientExpression;
        internal Expression<Func<TSource, TDestination>> donorExpression;

        protected override Expression VisitMemberInit(MemberInitExpression recipientInit)
        {
            if(recipientInit != recipientExpression.Body)
            {
                return base.VisitMemberInit(recipientInit);
            }

            VerifyNoArguments(recipientInit);

            var bindingsOfRecipient = recipientInit.Bindings
                         ?? throw new ArgumentException(GetPropperFormDescriptipn(recipientInit));

            var binddingsOfDonor = GetBindings(donorExpression);

            var visitedNew = (NewExpression) base.VisitNew(recipientInit.NewExpression);

            var combinedVisitedBindings = bindingsOfRecipient
                                                .Union(binddingsOfDonor)
                                                .Select(x => base.VisitMemberBinding(x))
                                                .ToArray();

            return Expression.MemberInit(visitedNew, combinedVisitedBindings);
        }

        internal static MemberBinding[] GetBindings(Expression<Func<TSource, TDestination>> source)
        {
            var body = source.Body as MemberInitExpression
                        ?? throw new ArgumentException(GetPropperFormDescriptipn(source));

            VerifyNoArguments(body);

            var bindingsCollection = body.Bindings
                            ?? throw new ArgumentException(GetPropperFormDescriptipn(source));

            var bindings = bindingsCollection.ToArray();

            return bindings;
        }

        internal static BinaryExpression 
            GetAssignmentFromBinding(MemberBinding binding,
                                     ParameterExpression paramSourceExisting,
                                     ParameterExpression paramSourceNew,
                                     ParameterExpression paramDestination)
        {
            var existingAssignment = binding as MemberAssignment
                                ?? throw new ArgumentException("Only binding of type MemberAssignment can be used. " +
                                                                $"Received binding of type {binding.BindingType}, {binding.ToString()}");

            var targetMember = Expression.MakeMemberAccess(paramDestination, existingAssignment.Member);

            var newExpression = new ParameterReplacerVisitor(paramSourceExisting, paramSourceNew)
                                            .Visit(existingAssignment.Expression);

            var assignment = Expression.Assign(targetMember, newExpression);

            return assignment;
        }

        private static void VerifyNoArguments(MemberInitExpression body)
        {
            if (body.NewExpression?.Arguments?.Any() == true)
            {
                throw new ArgumentException(GetPropperFormDescriptipn(body));
            }
        }

        private static string GetPropperFormDescriptipn(Expression source)
        {
            return
                "Mappings should be in the form of parameterless cunstructor " +
                "with object initializer like \r\n" +
                "(x) => new Foo \r\n" +
                "{ \r\n" +
                "A = x.A \r\n" +
                "} \r\n" +
                $"Invalid mapping expression: {source.ToString()}";
        }
    }

    internal class ParameterReplacerVisitor : ExpressionVisitor
    {
        ParameterExpression existing;
        ParameterExpression @new;

        public ParameterReplacerVisitor(ParameterExpression existing, ParameterExpression @new)
        {
            this.existing = existing;
            this.@new = @new;
        }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            if (node == existing)
            {
                return @new;
            }

            return base.VisitParameter(node);
        }
    }
}