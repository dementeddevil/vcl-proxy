using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using Im.Proxy.VclCore.Model;
// ReSharper disable AssignNullToNotNullAttribute

namespace Im.Proxy.VclCore.Compiler
{
    public class VclCompileNamedSubroutine : VclBaseExpressionVisitor
    {
        private ParameterExpression _vclContextExpression;
        private IList<Expression> _currentMethodStatementExpressions;

        private Expression _currentMemberAccessExpression;
        private int _memberAccessDepth;

        public IDictionary<string, IList<Expression>> MethodBodyExpressions { get; } =
            new Dictionary<string, IList<Expression>>(StringComparer.OrdinalIgnoreCase);

        public IDictionary<string, Expression> AclFields { get; }

        public override Expression VisitAclDeclaration(VclParser.AclDeclarationContext context)
        {
            return null;
        }

        public override Expression VisitBackendDeclaration(VclParser.BackendDeclarationContext context)
        {
            return null;
        }

        public override Expression VisitProbeDeclaration(VclParser.ProbeDeclarationContext context)
        {
            return null;
        }

        public override Expression VisitProcedureDeclaration(VclParser.ProcedureDeclarationContext context)
        {
            _currentMethodStatementExpressions = new List<Expression>();

            base.VisitProcedureDeclaration(context);

            var name = context.name.Text;
            MethodBodyExpressions.Add(name, _currentMethodStatementExpressions);

            return null;
        }

        public override Expression VisitSimpleReturnStateExpression(VclParser.SimpleReturnStateExpressionContext context)
        {
            base.VisitSimpleReturnStateExpression(context);

            var action = context.GetText().Replace("-", "");
            return Expression.Return(null, Expression.Constant(
                Enum.Parse(typeof(VclAction), action, true)));
        }

        public override Expression VisitComplexReturnStateExpression(VclParser.ComplexReturnStateExpressionContext context)
        {
            base.VisitComplexReturnStateExpression(context);

            // Setup delivery text body and suitable status code
            return null;
        }

        public override Expression VisitExpressionStatement(VclParser.ExpressionStatementContext context)
        {
            base.VisitExpressionStatement(context);

            if (context.expression() != null)
            {
                _currentMethodStatementExpressions.Add(
                    VisitExpression(context.expression()));
            }

            return null;
        }

        public override Expression VisitAssignmentExpression(VclParser.AssignmentExpressionContext context)
        {
            base.VisitAssignmentExpression(context);

            var lhs = VisitUnaryExpression(context.lhs);
            var rhs = VisitExpression(context.rhs);

            if (lhs.Type != rhs.Type)
            {
                rhs = ConvertExpression(rhs, lhs.Type);
            }

            switch (context.op.GetText())
            {
                case "=":
                    return Expression.Assign(lhs, rhs);

                case "+=":
                    return Expression.AddAssign(lhs, rhs);

                case "-=":
                    return Expression.SubtractAssign(lhs, rhs);

                default:
                    throw new InvalidOperationException("Unexpected assignment operator encountered");
            }
        }

        public override Expression VisitConditionalExpression(VclParser.ConditionalExpressionContext context)
        {
            base.VisitConditionalExpression(context);

            var result = VisitConditionalOrExpression(context.conditionalOrExpression());
            if (context.ifTrue != null && context.ifFalse != null)
            {
                result = Expression.Condition(
                    result,
                    VisitExpression(context.ifTrue),
                    VisitExpression(context.ifFalse));
            }

            return result;
        }

        public override Expression VisitConditionalOrExpression(VclParser.ConditionalOrExpressionContext context)
        {
            base.VisitConditionalOrExpression(context);

            return Expression.OrElse(
                VisitConditionalAndExpression(context.lhs),
                VisitConditionalAndExpression(context.rhs));
        }

        public override Expression VisitConditionalAndExpression(VclParser.ConditionalAndExpressionContext context)
        {
            base.VisitConditionalAndExpression(context);

            return Expression.AndAlso(
                VisitInclusiveOrExpression(context.lhs),
                VisitInclusiveOrExpression(context.rhs));
        }

        public override Expression VisitInclusiveOrExpression(VclParser.InclusiveOrExpressionContext context)
        {
            base.VisitInclusiveOrExpression(context);

            return Expression.Or(
                VisitExclusiveOrExpression(context.lhs),
                VisitExclusiveOrExpression(context.rhs));
        }

        public override Expression VisitExclusiveOrExpression(VclParser.ExclusiveOrExpressionContext context)
        {
            base.VisitExclusiveOrExpression(context);

            return Expression.ExclusiveOr(
                VisitAndExpression(context.lhs),
                VisitAndExpression(context.rhs));
        }

        public override Expression VisitAndExpression(VclParser.AndExpressionContext context)
        {
            base.VisitAndExpression(context);

            return Expression.And(
                VisitEqualityExpression(context.lhs),
                VisitEqualityExpression(context.rhs));
        }

        public override Expression VisitEqualStandardExpression(VclParser.EqualStandardExpressionContext context)
        {
            base.VisitEqualStandardExpression(context);

            switch (context.op.Text)
            {
                case "==":
                    return Expression.Equal(
                        VisitRelationalExpression(context.lhs),
                        VisitRelationalExpression(context.rhs));

                case "!=":
                    return Expression.NotEqual(
                        VisitRelationalExpression(context.lhs),
                        VisitRelationalExpression(context.rhs));

                default:
                    throw new ArgumentException("Unknown equality operator encountered");
            }
        }

        public override Expression VisitMatchRegexExpression(VclParser.MatchRegexExpressionContext context)
        {
            base.VisitMatchRegexExpression(context);

            // Get regular expression from RHS context
            var regexExpression = VisitRegularExpression(context.rhs);

            var lhs = VisitRelationalExpression(context.lhs);

            // LHS expression needs to be string
            if (lhs.Type != typeof(string))
            {
                lhs = Expression.Call(
                    lhs,
                    typeof(object).GetMethod(
                        nameof(ToString),
                        BindingFlags.Static | BindingFlags.Public));
            }

            // Build call to VclAcl.IsMatch method
            Expression result = Expression.Call(
                regexExpression,
                typeof(Regex).GetMethod(
                    nameof(Regex.IsMatch),
                    new[] { typeof(string) }),
                lhs);

            // Handle negation of the result as necessary
            if (context.op.Text == "!~")
            {
                result = Expression.Negate(result);
            }

            return result;
        }

        public override Expression VisitRegularExpression(VclParser.RegularExpressionContext context)
        {
            base.VisitRegularExpression(context);

            var expr = context.StringConstant().GetText().Trim('"');

            // TODO: Convert from PRCE regex to .NET regex

            // For now you'll need to do this by hand
            return Expression.New(
                typeof(Regex).GetConstructor(
                    new[] { typeof(string), typeof(RegexOptions) }),
                Expression.Constant(expr),
                Expression.Constant(
                    RegexOptions.Singleline |
                    RegexOptions.Compiled |
                    RegexOptions.IgnoreCase));
        }

        public override Expression VisitMatchAclExpression(VclParser.MatchAclExpressionContext context)
        {
            base.VisitMatchAclExpression(context);

            var lhs = VisitRelationalExpression(context.lhs);

            // Add code to convert LHS string into IPAddress
            if (lhs.Type == typeof(string))
            {
                lhs = Expression.Call(
                    null,
                    typeof(IPAddress).GetMethod(
                        nameof(IPAddress.Parse),
                        BindingFlags.Static |
                        BindingFlags.Public),
                    lhs);
            }

            // Throw if LHS type is not an IPAddress
            if (lhs.Type != typeof(IPAddress))
            {
                throw new ArgumentException("Left expression must resolve to string or IPAddress");
            }

            // Build call to VclAcl.IsMatch method
            Expression result = Expression.Call(
                AclFields[context.rhs.GetText()],
                typeof(VclAcl).GetMethod(
                    nameof(VclAcl.IsMatch),
                    new[] { typeof(IPAddress) }),
                lhs);

            // Handle negation of the result as necessary
            if (context.op.Text == "!~")
            {
                result = Expression.Negate(result);
            }

            return result;
        }

        public override Expression VisitRelationalExpression(VclParser.RelationalExpressionContext context)
        {
            base.VisitRelationalExpression(context);

            switch (context.op.Text)
            {
                case "<":
                    return Expression.LessThan(
                        VisitAdditiveExpression(context.lhs),
                        VisitAdditiveExpression(context.rhs));

                case ">":
                    return Expression.GreaterThan(
                        VisitAdditiveExpression(context.lhs),
                        VisitAdditiveExpression(context.rhs));

                case "<=":
                    return Expression.LessThanOrEqual(
                        VisitAdditiveExpression(context.lhs),
                        VisitAdditiveExpression(context.rhs));

                case ">=":
                    return Expression.GreaterThanOrEqual(
                        VisitAdditiveExpression(context.lhs),
                        VisitAdditiveExpression(context.rhs));

                default:
                    throw new ArgumentException("Unknown relational operator encountered");
            }
        }

        public override Expression VisitAdditiveExpression(VclParser.AdditiveExpressionContext context)
        {
            base.VisitAdditiveExpression(context);

            switch (context.op.Text)
            {
                case "+":
                    return Expression.Add(
                        VisitMultiplicativeExpression(context.lhs),
                        VisitMultiplicativeExpression(context.rhs));

                case "-":
                    return Expression.Subtract(
                        VisitMultiplicativeExpression(context.lhs),
                        VisitMultiplicativeExpression(context.rhs));

                default:
                    throw new ArgumentException("Unknown relational operator encountered");
            }
        }

        public override Expression VisitMultiplicativeExpression(VclParser.MultiplicativeExpressionContext context)
        {
            base.VisitMultiplicativeExpression(context);

            switch (context.op.Text)
            {
                case "*":
                    return Expression.Multiply(
                        VisitUnaryExpression(context.lhs),
                        VisitUnaryExpression(context.rhs));

                case "/":
                    return Expression.Divide(
                        VisitUnaryExpression(context.lhs),
                        VisitUnaryExpression(context.rhs));

                case "%":
                    return Expression.Modulo(
                        VisitUnaryExpression(context.lhs),
                        VisitUnaryExpression(context.rhs));

                default:
                    throw new ArgumentException("Unknown relational operator encountered");
            }
        }

        public override Expression VisitUnaryNegateExpression(VclParser.UnaryNegateExpressionContext context)
        {
            base.VisitUnaryNegateExpression(context);

            return Expression.Negate(VisitUnaryExpression(context.unaryExpression()));
        }

        public override Expression VisitMemberAccessExpression(VclParser.MemberAccessExpressionContext context)
        {
            if (_memberAccessDepth == 0)
            {
                var identifier = context.lhs.Text;
                switch (identifier)
                {
                    case "client":
                        _currentMemberAccessExpression =
                            Expression.MakeMemberAccess(
                                _vclContextExpression,
                                typeof(VclContext).GetProperty(nameof(VclContext.Client)));
                        break;
                    case "server":
                        _currentMemberAccessExpression =
                            Expression.MakeMemberAccess(
                                _vclContextExpression,
                                typeof(VclContext).GetProperty(nameof(VclContext.Server)));
                        break;
                    case "req":
                        _currentMemberAccessExpression =
                            Expression.MakeMemberAccess(
                                _vclContextExpression,
                                typeof(VclContext).GetProperty(nameof(VclContext.Request)));
                        break;
                    case "resp":
                        _currentMemberAccessExpression =
                            Expression.MakeMemberAccess(
                                _vclContextExpression,
                                typeof(VclContext).GetProperty(nameof(VclContext.Response)));
                        break;
                    case "bereq":
                        _currentMemberAccessExpression =
                            Expression.MakeMemberAccess(
                                _vclContextExpression,
                                typeof(VclContext).GetProperty(nameof(VclContext.BackendRequest)));
                        break;
                    case "beresp":
                        _currentMemberAccessExpression =
                            Expression.MakeMemberAccess(
                                _vclContextExpression,
                                typeof(VclContext).GetProperty(nameof(VclContext.BackendResponse)));
                        break;

                    default:
                        throw new ArgumentException("Unable to determine top-level object");
                }
            }

            var rhsIdentifier = context.rhs.Text;

            // Combine the current access expression with the right-hand-side
            // Special case for dictionary on LHS
            if (_currentMemberAccessExpression.Type == typeof(IDictionary<string, string>))
            {
                _currentMemberAccessExpression = Expression.MakeIndex(
                    _currentMemberAccessExpression,
                    typeof(IDictionary<string, string>).GetProperty("Item"),
                    new[] { Expression.Constant(rhsIdentifier) });
            }
            else
            {
                _currentMemberAccessExpression = Expression.MakeMemberAccess(
                    _currentMemberAccessExpression,
                    _currentMemberAccessExpression.Type.GetProperty(rhsIdentifier));
            }

            ++_memberAccessDepth;
            try
            {
                base.VisitMemberAccessExpression(context);
            }
            finally
            {
                --_memberAccessDepth;
            }

            return _currentMemberAccessExpression;
        }

        private Expression ConvertExpression(Expression expr, Type targetType)
        {
            // Converting to string is easy...
            if (targetType == typeof(string))
            {
                return Expression.Call(
                    expr,
                    typeof(object).GetMethod("ToString"));
            }

            if (targetType == typeof(bool))
            {
                if (expr.Type == typeof(int))
                {
                    // Zero is falsey
                    return Expression.NotEqual(expr, Expression.Constant(0));
                }

                if (expr.Type == typeof(string))
                {
                    // Empty strings are falsey
                    return Expression.Negate(
                        Expression.Call(
                            typeof(string).GetMethod(
                                nameof(string.IsNullOrWhiteSpace),
                                new[] { typeof(string) }),
                            expr));
                }
            }

            throw new NotSupportedException("Invalid cast exception");
        }
    }
}