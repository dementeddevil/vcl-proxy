using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Net;
using System.Reflection;
using System.Reflection.Emit;
using System.Text.RegularExpressions;
using Im.Proxy.VclCore.Model;
// ReSharper disable AssignNullToNotNullAttribute

namespace Im.Proxy.VclCore.Compiler
{
    public class VclCompileNamedSubroutine : VclBaseExpressionVisitor
    {
        private class VclContextObjectMapper
        {
            private class VclObjectMemberMapper
            {
                private readonly PropertyInfo _contextPropertyInfo;
                private readonly IDictionary<string, Lazy<PropertyInfo>> _mapper =
                    new Dictionary<string, Lazy<PropertyInfo>>(StringComparer.OrdinalIgnoreCase);

                public VclObjectMemberMapper(
                    PropertyInfo contextPropertyInfo,
                    Type vclObjectType,
                    params Tuple<string, string>[] grammarToLogicalEntries)
                {
                    _contextPropertyInfo = contextPropertyInfo;
                    foreach (var item in grammarToLogicalEntries)
                    {
                        _mapper.Add(
                            item.Item1,
                            new Lazy<PropertyInfo>(() => vclObjectType.GetProperty(item.Item2)));
                    }
                }

                public Expression MakeAccessExpression(Expression vclContextObjectExpression, string memberName)
                {
                    if (!_mapper.ContainsKey(memberName))
                    {
                        throw new ArgumentException("context member not found.");
                    }

                    return Expression.MakeMemberAccess(
                        Expression.MakeMemberAccess(
                            vclContextObjectExpression,
                            _contextPropertyInfo),
                        _mapper[memberName].Value);
                }
            }

            private readonly IDictionary<string, VclObjectMemberMapper> _contextVariableToMapper =
                new Dictionary<string, VclObjectMemberMapper>(StringComparer.OrdinalIgnoreCase);

            public VclContextObjectMapper()
            {
                _contextVariableToMapper.Add(
                    "client",
                    new VclObjectMemberMapper(
                        typeof(VclContext).GetProperty(nameof(VclContext.Client)),
                        typeof(VclClient),
                        new[]
                        {
                            new Tuple<string, string>("", ""), 
                        }));
                _contextVariableToMapper.Add(
                    "server",
                    new VclObjectMemberMapper(
                        typeof(VclContext).GetProperty(nameof(VclContext.Server)),
                        typeof(VclServer),
                        new[]
                        {
                            new Tuple<string, string>("", ""),
                        }));
                _contextVariableToMapper.Add(
                    "req",
                    new VclObjectMemberMapper(
                        typeof(VclContext).GetProperty(nameof(VclContext.Request)),
                        typeof(VclRequest),
                        new[]
                        {
                            new Tuple<string, string>("", ""),
                        }));
                _contextVariableToMapper.Add(
                    "resp",
                    new VclObjectMemberMapper(
                        typeof(VclContext).GetProperty(nameof(VclContext.Response)),
                        typeof(VclResponse),
                        new[]
                        {
                            new Tuple<string, string>("", ""),
                        }));
                _contextVariableToMapper.Add(
                    "bereq",
                    new VclObjectMemberMapper(
                        typeof(VclContext).GetProperty(nameof(VclContext.BackendRequest)),
                        typeof(VclBackendRequest),
                        new[]
                        {
                            new Tuple<string, string>("", ""),
                        }));
                _contextVariableToMapper.Add(
                    "beresp",
                    new VclObjectMemberMapper(
                        typeof(VclContext).GetProperty(nameof(VclContext.BackendResponse)),
                        typeof(VclBackendResponse),
                        new[]
                        {
                            new Tuple<string, string>("", ""),
                        }));
            }

            public bool TryGetExpression(
                Expression vclContextExpression,
                string objectName,
                string memberName,
                out Expression expression)
            {
                if (!_contextVariableToMapper.TryGetValue(objectName, out var mapper))
                {
                    expression = null;
                    return false;
                }

                expression = mapper.MakeAccessExpression(vclContextExpression, memberName);
                return true;
            }

            public Expression GetExpression(
                Expression vclContextExpression,
                string objectName,
                string memberName)
            {
                if (TryGetExpression(
                    vclContextExpression,
                    objectName,
                    memberName,
                    out var expression))
                {
                    return expression;
                }

                throw new ArgumentException("Invalid VCL object name.");
            }
        }

        private readonly VclContextObjectMapper _contextObjectMapper = new VclContextObjectMapper();
        private ParameterExpression _vclContextExpression;
        private List<Expression> _currentMethodStatementExpressions;

        private Expression _currentMemberAccessExpression;
        private int _memberAccessDepth;
        private TypeBuilder _derivedVclHandlerBuilder;

        public IDictionary<string, MethodBuilder> SubroutineMethodBuilders { get; } =
            new Dictionary<string, MethodBuilder>(StringComparer.OrdinalIgnoreCase);

        public IDictionary<string, List<Expression>> MethodBodyExpressions { get; } =
            new Dictionary<string, List<Expression>>(StringComparer.OrdinalIgnoreCase);

        public IDictionary<string, Expression> AclFields { get; }

        public override Expression VisitProcedureDeclaration(VclParser.ProcedureDeclarationContext context)
        {
            _currentMethodStatementExpressions = new List<Expression>();

            base.VisitProcedureDeclaration(context);

            var name = context.name.Text;

            if (MethodBodyExpressions.ContainsKey(name))
            {
                MethodBodyExpressions[name].AddRange(_currentMethodStatementExpressions);
            }
            else
            {
                MethodBodyExpressions.Add(name, _currentMethodStatementExpressions);
            }

            return null;
        }

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

        public override Expression VisitIfStatement(VclParser.IfStatementContext context)
        {
            base.VisitIfStatement(context);
            if (context.otherTest.IsEmpty && context.elseStmt.IsEmpty)
            {
                return Expression.IfThen(
                    VisitExpression(context.test),
                    VisitStatement(context.ifTrue));
            }

            if (context.otherTest.IsEmpty)
            {
                return Expression.IfThenElse(
                    VisitExpression(context.test),
                    VisitStatement(context.ifTrue),
                    VisitStatement(context.elseStmt));
            }

            return Expression.IfThenElse(
                VisitExpression(context.test),
                VisitStatement(context.ifTrue),
                Expression.IfThenElse(
                    VisitExpression(context.otherTest),
                    VisitStatement(context.otherTrue),
                    VisitStatement(context.elseStmt)));
        }

        public override Expression VisitSetStatement(VclParser.SetStatementContext context)
        {
            base.VisitSetStatement(context);
            var lhs = VisitMemberAccessExpression(context.lhs);
            var rhs = VisitExpression(context.rhs);
            return Expression.Assign(lhs, rhs);
        }

        public override Expression VisitRemoveStatement(VclParser.RemoveStatementContext context)
        {
            base.VisitRemoveStatement(context);
            var lhs = VisitMemberAccessExpression(context.id);
            var rhs = Expression.Constant(null, typeof(string));
            return Expression.Assign(lhs, rhs);
        }

        public override Expression VisitErrorStatement(VclParser.ErrorStatementContext context)
        {
            // Parse status code (from int or HttpStatusCode enum value)
            var statusCodeText = context.statusCode.Text;
            if (!int.TryParse(statusCodeText, out int statusCode))
            {
                if (!Enum.TryParse(statusCodeText, true, out HttpStatusCode statausCodeEnum))
                {
                    throw new ArgumentException("Unable to parse status code");
                }

                statusCode = (int)statausCodeEnum;
            }

            var statusDescription = context.statusDescription?.Text ?? string.Empty;

            // Error implies instant response
            return Expression.Block(
                Expression.Assign(
                    _contextObjectMapper.GetExpression(
                        _vclContextExpression,
                        "resp",
                        "statusCode"),
                    Expression.Constant(statusCode)),
                Expression.Assign(
                    _contextObjectMapper.GetExpression(
                        _vclContextExpression,
                        "resp",
                        "description"),
                    Expression.Constant(statusDescription)),
                Expression.Return(null, Expression.Constant(
                    VclAction.DeliverContent)));
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
                var memberName = context.rhs.Text;

                // Lookup based on date
                if (identifier == "now" && memberName == null)
                {
                    // TODO: We might want to delegate this call through a helper
                    //  to facilitate reliable testing of datetime constructs
                    return Expression.MakeMemberAccess(
                        null,
                        typeof(DateTime).GetProperty(
                            "UtcNow",
                            BindingFlags.Static |
                            BindingFlags.Public));
                }

                // Finally attempt to perform lookup for context variables
                if (!_contextObjectMapper.TryGetExpression(
                    _vclContextExpression, 
                    identifier,
                    memberName,
                    out var expression))
                {
                    throw new ArgumentException("Unable to determine top-level object");
                }

                return expression;
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