using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Net;
using System.Reflection;
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
                private readonly string _logicalName;
                private readonly IDictionary<string, (string, Type)> _mapper;

                public VclObjectMemberMapper(
                    string logicalName,
                    IDictionary<string, (string, Type)> grammarToLogicalEntries)
                {
                    _logicalName = logicalName;
                    _mapper = new Dictionary<string, (string, Type)>(
                        grammarToLogicalEntries,
                        StringComparer.OrdinalIgnoreCase);
                }

                public CodeExpression MakeAccessExpression(
                    CodeExpression vclContextExpression, string memberName)
                {
                    if (!_mapper.ContainsKey(memberName))
                    {
                        throw new ArgumentException("context member not found.");
                    }

                    return new CodePropertyReferenceExpression(
                            new CodePropertyReferenceExpression(
                                vclContextExpression, _logicalName),
                            _mapper[memberName].Item1)
                        .SetExpressionType(_mapper[memberName].Item2);
                }
            }

            private readonly IDictionary<string, VclObjectMemberMapper> _contextVariableToMapper =
                new Dictionary<string, VclObjectMemberMapper>(StringComparer.OrdinalIgnoreCase);

            public VclContextObjectMapper()
            {
                _contextVariableToMapper.Add(
                    "local",
                    new VclObjectMemberMapper(
                        nameof(VclContext.Local),
                        new Dictionary<string, (string, Type)>
                        {
                            { "ip", ("Ip", typeof(string)) },
                            { "endpoint", ("Endpoint", typeof(string)) },
                            { "socket", ("Socket", typeof(string)) }
                        }));
                _contextVariableToMapper.Add(
                    "remote",
                    new VclObjectMemberMapper(
                        nameof(VclContext.Remote),
                        new Dictionary<string, (string, Type)>
                        {
                            { "ip", ("Ip", typeof(string)) }
                        }));
                _contextVariableToMapper.Add(
                    "client",
                    new VclObjectMemberMapper(
                        nameof(VclContext.Client),
                        new Dictionary<string, (string, Type)>
                        {
                            { "ip", ("Ip", typeof(string)) },
                            { "identity", ("Identity", typeof(string)) }
                        }));
                _contextVariableToMapper.Add(
                    "server",
                    new VclObjectMemberMapper(
                        nameof(VclContext.Server),
                        new Dictionary<string, (string, Type)>
                        {
                            { "ip", ("Ip", typeof(string)) },
                            { "hostnam", ("HostName", typeof(string)) },
                            { "identity", ("Identity", typeof(string)) },
                            { "port", ("Port", typeof(int)) }
                        }));
                _contextVariableToMapper.Add(
                    "req",
                    new VclObjectMemberMapper(
                        nameof(VclContext.Request),
                        new Dictionary<string, (string, Type)>
                        {
                            { "method", ("Method", typeof(string)) },
                            { "url", ("Url", typeof(string)) },
                            { "http", ("Headers", typeof(IDictionary<string, string>)) },
                            { "proto", ("ProtocolVersion", typeof(string)) },
                            { "hash", ("Hash", typeof(string)) },
                            { "backend_hint", ("Backend", typeof(VclBackend)) },
                            { "restarts", ("Restarts", typeof(int)) },
                            { "esi_level", ("EsiLevel", typeof(int)) },
                            { "ttl", ("Ttl", typeof(TimeSpan)) },
                            { "xid", ("RequestId", typeof(string)) },
                            { "can_gzip", ("CanGzip", typeof(bool)) },
                            { "hash_always_miss", ("HashAlwaysMiss", typeof(bool)) },
                            { "hash_ignore_busy", ("HashIgnoreBusy", typeof(bool)) }
                        }));
                _contextVariableToMapper.Add(
                    "req_top",
                    new VclObjectMemberMapper(
                        nameof(VclContext.TopRequest),
                        new Dictionary<string, (string, Type)>
                        {
                            { "method", ("Method", typeof(string)) },
                            { "url", ("Url", typeof(string)) },
                            { "http", ("Headers", typeof(IDictionary<string, string>)) },
                            { "proto", ("ProtocolVersion", typeof(string)) }
                        }));
                _contextVariableToMapper.Add(
                    "bereq",
                    new VclObjectMemberMapper(
                        nameof(VclContext.BackendRequest),
                        new Dictionary<string, (string, Type)>
                        {
                            { "", ("", typeof(string)) }
                        }));
                _contextVariableToMapper.Add(
                    "beresp",
                    new VclObjectMemberMapper(
                        nameof(VclContext.BackendResponse),
                        new Dictionary<string, (string, Type)>
                        {
                            { "", ("", typeof(string)) }
                        }));
                _contextVariableToMapper.Add(
                    "obj",
                    new VclObjectMemberMapper(
                        nameof(VclContext.Object),
                        new Dictionary<string, (string, Type)>
                        {
                            { "", ("", typeof(string)) }
                        }));
                _contextVariableToMapper.Add(
                    "resp",
                    new VclObjectMemberMapper(
                        nameof(VclContext.Response),
                        new Dictionary<string, (string, Type)>
                        {
                            { "", ("", typeof(string)) }
                        }));
            }

            public bool TryGetExpression(
                CodeExpression vclContextExpression,
                string objectName,
                string memberName,
                out CodeExpression expression)
            {
                if (!_contextVariableToMapper.TryGetValue(objectName, out var mapper))
                {
                    expression = null;
                    return false;
                }

                expression = mapper.MakeAccessExpression(vclContextExpression, memberName);
                return true;
            }

            public CodeExpression GetExpression(
                CodeExpression vclContextExpression,
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

        private readonly Stack<CodeConditionStatement> _currentCompoundStatementExpressions = new Stack<CodeConditionStatement>();
        private readonly Stack<IDictionary<string, CodeVariableReferenceExpression>> _currentCompoundStatementVariables =
            new Stack<IDictionary<string, CodeVariableReferenceExpression>>();

        private int _unnamedVariableIndex;

        public VclCompileNamedSubroutine(VclCompilerContext compilerContext)
            : base(compilerContext)
        {
        }

        public override CodeObject VisitSystemProcedureDeclaration(VclParser.SystemProcedureDeclarationContext context)
        {
            // Get or create method body expression
            var name = context.name.Text;
            CompilerContext.GetOrCreateSystemMethodStatements(name)
                .Add((CodeStatement)VisitCompoundStatement(context.compoundStatement()));
            return null;
        }

        public override CodeObject VisitCustomProcedureDeclaration(VclParser.CustomProcedureDeclarationContext context)
        {
            // Create method body expression
            var name = context.name.Text;
            CompilerContext.CreateCustomMethodStatements(name)
                .Add((CodeStatement)VisitCompoundStatement(context.compoundStatement()));
            return null;
        }

        public override CodeObject VisitAclDeclaration(VclParser.AclDeclarationContext context)
        {
            return null;
        }

        public override CodeObject VisitBackendDeclaration(VclParser.BackendDeclarationContext context)
        {
            return null;
        }

        public override CodeObject VisitProbeDeclaration(VclParser.ProbeDeclarationContext context)
        {
            return null;
        }

        public override CodeObject VisitCallStatement(VclParser.CallStatementContext context)
        {
            var subroutineName = context.subroutineName.Text;
            if (subroutineName.StartsWith("vcl_", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Cannot make explicit calls to reserved methods.");
            }

            var localVariableName = GetUniqueLocalVariableName();
            _currentCompoundStatementExpressions.Peek()
                .TrueStatements.Add(new CodeVariableDeclarationStatement(
                    typeof(VclFrontendAction), localVariableName));
            var localVariableReference = new CodeVariableReferenceExpression(localVariableName);

            _currentCompoundStatementExpressions.Peek()
                .TrueStatements.AddRange(
                    new CodeStatement[]
                    {
                        new CodeAssignStatement(
                            localVariableReference,
                            new CodeMethodInvokeExpression(
                                new CodeMethodReferenceExpression(
                                    new CodeThisReferenceExpression(),
                                    subroutineName),
                                new CodeVariableReferenceExpression("context"))),
                        new CodeConditionStatement(
                            new CodeBinaryOperatorExpression(
                                localVariableReference,
                                CodeBinaryOperatorType.IdentityInequality,
                                new CodePrimitiveExpression(VclFrontendAction.NoOp)),
                            new CodeMethodReturnStatement(localVariableReference))
                    });

            return base.VisitCallStatement(context);
        }

        public override CodeObject VisitRestartStatement(VclParser.RestartStatementContext context)
        {
            return new CodeMethodReturnStatement(
                new CodePrimitiveExpression(VclFrontendAction.Restart));
        }

        public override CodeObject VisitReturnStatement(VclParser.ReturnStatementContext context)
        {
            // If we have no return state then we must be in a custom function
            if (context.returnStateExpression() == null)
            {
                // What we actually return here is VclAction.NoOp
                return new CodeMethodReturnStatement(
                    new CodePrimitiveExpression(VclFrontendAction.NoOp));
            }

            // Otherwise call through base class
            return base.VisitReturnStatement(context);
        }

        public override CodeObject VisitSimpleReturnStateExpression(VclParser.SimpleReturnStateExpressionContext context)
        {
            base.VisitSimpleReturnStateExpression(context);

            var action = context.GetText().Replace("-", string.Empty);
            return new CodeMethodReturnStatement(
                new CodePrimitiveExpression(Enum.Parse(typeof(VclFrontendAction), action, true)));
        }

        public override CodeObject VisitComplexReturnStateExpression(VclParser.ComplexReturnStateExpressionContext context)
        {
            base.VisitComplexReturnStateExpression(context);

            // TODO: Setup delivery text body and suitable status code
            return null;
        }

        public override CodeObject VisitHashDataStatement(VclParser.HashDataStatementContext context)
        {
            // Get expression to add to the hash
            var expr = (CodeExpression)VisitExpression(context.expr);

            // Return method call to context.Request.AddToHash
            return new CodeMethodInvokeExpression(
                new CodePropertyReferenceExpression(
                    new CodeArgumentReferenceExpression("context"),
                    "Request"),
                "AddToHash",
                expr);
        }

        public override CodeObject VisitCompoundStatement(VclParser.CompoundStatementContext context)
        {
            _currentCompoundStatementVariables.Push(
                new Dictionary<string, CodeVariableReferenceExpression>(StringComparer.OrdinalIgnoreCase));
            _currentCompoundStatementExpressions.Push(
                new CodeConditionStatement
                {
                    Condition = new CodePrimitiveExpression(true)
                });
            try
            {
                // Build compound statement and return block expression
                base.VisitCompoundStatement(context);
                return _currentCompoundStatementExpressions.Peek();
            }
            finally
            {
                _currentCompoundStatementVariables.Pop();
                _currentCompoundStatementExpressions.Pop();
            }
        }

        public override CodeObject VisitStatement(VclParser.StatementContext context)
        {
            var expression = base.VisitStatement(context);
            if (expression != null)
            {
                _currentCompoundStatementExpressions.Peek().TrueStatements.Add((CodeStatement)expression);
            }
            return expression;
        }

        public override CodeObject VisitVarStatement(VclParser.VarStatementContext context)
        {
            var name = context.name.Text.Substring(4).SafeIdentifier("local");

            // Variable cannot already be declared in this scope or any parent scope
            foreach (var scope in _currentCompoundStatementVariables)
            {
                if (scope.ContainsKey(name))
                {
                    throw new ArgumentException(
                        "Variable with same name is already declared in this scope or a parent scope.");
                }
            }

            base.VisitVarStatement(context);

            // Parse type
            var typeName = context.type.Text;
            Type type;
            CodePrimitiveExpression initialValueExpression;
            switch (typeName)
            {
                case "BOOL":
                    type = typeof(bool);
                    initialValueExpression = new CodePrimitiveExpression(false);
                    break;
                case "INTEGER":
                    type = typeof(int);
                    initialValueExpression = new CodePrimitiveExpression(0);
                    break;
                case "FLOAT":
                    type = typeof(double);
                    initialValueExpression = new CodePrimitiveExpression(0.0);
                    break;
                case "TIME":
                    type = typeof(DateTime);
                    initialValueExpression = new CodePrimitiveExpression(DateTime.Now);
                    break;
                case "RTIME":
                    type = typeof(TimeSpan);
                    initialValueExpression = new CodePrimitiveExpression(TimeSpan.Zero);
                    break;
                case "STRING":
                    type = typeof(string);
                    initialValueExpression = new CodePrimitiveExpression(null);
                    break;
                default:
                    throw new InvalidOperationException("Unexpected variable type encountered");
            }

            // Create variable expression and save in current scope
            var definitionExpression =
                new CodeVariableDeclarationStatement(type, name)
                {
                    InitExpression = initialValueExpression
                };
            _currentCompoundStatementExpressions.Peek().TrueStatements.Add(definitionExpression);

            var referenceExpression = new CodeVariableReferenceExpression(name);
            _currentCompoundStatementVariables.Peek()[name] = referenceExpression;

            return referenceExpression;
        }

        public override CodeObject VisitIfStatement(VclParser.IfStatementContext context)
        {
            base.VisitIfStatement(context);
            if (context.otherTest.IsEmpty && context.elseStmt.IsEmpty)
            {
                return new CodeConditionStatement(
                    (CodeExpression)VisitConditionalOrExpression(context.test),
                    (CodeStatement)VisitStatement(context.ifTrue));
            }

            if (context.otherTest.IsEmpty)
            {
                return new CodeConditionStatement(
                    (CodeExpression)VisitConditionalOrExpression(context.test),
                    (CodeStatement)VisitStatement(context.ifTrue),
                    (CodeStatement)VisitStatement(context.elseStmt));
            }

            return new CodeConditionStatement(
                (CodeExpression)VisitConditionalOrExpression(context.test),
                new[]
                {
                    (CodeStatement)VisitStatement(context.ifTrue),
                },
                new[]
                {
                    (CodeStatement) new CodeConditionStatement(
                        (CodeExpression)VisitConditionalOrExpression(context.otherTest),
                        new[]
                        {
                            (CodeStatement)VisitStatement(context.otherTrue)
                        },
                        new[]
                        {
                            (CodeStatement)VisitStatement(context.elseStmt)
                        })
                });
        }

        public override CodeObject VisitSetStatement(VclParser.SetStatementContext context)
        {
            base.VisitSetStatement(context);
            var lhs = VisitMemberAccessExpression(context.lhs);
            var rhs = VisitExpression(context.rhs);
            return new CodeAssignStatement(
                (CodeExpression)lhs,
                (CodeExpression)rhs);
        }

        public override CodeObject VisitRemoveStatement(VclParser.RemoveStatementContext context)
        {
            base.VisitRemoveStatement(context);
            var lhs = (CodeExpression)VisitMemberAccessExpression(context.id);
            // TODO: Ensure we match type with LHS
            var rhs = new CodePrimitiveExpression(null);
            return new CodeAssignStatement(lhs, rhs);
        }

        public override CodeObject VisitErrorStatement(VclParser.ErrorStatementContext context)
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
            _currentCompoundStatementExpressions.Peek().TrueStatements.AddRange(
                new CodeStatement[]
                {
                    new CodeAssignStatement(
                        _contextObjectMapper.GetExpression(
                            new CodeArgumentReferenceExpression("context"),
                            "resp",
                            "statusCode"),
                        new CodePrimitiveExpression(statusCode)),
                    new CodeAssignStatement(
                        _contextObjectMapper.GetExpression(
                            new CodeArgumentReferenceExpression("context"),
                            "resp",
                            "description"),
                        new CodePrimitiveExpression(statusDescription)),
                });
            return new CodeMethodReturnStatement(
                new CodePrimitiveExpression(VclFrontendAction.DeliverContent));
        }

        public override CodeObject VisitExpressionStatement(VclParser.ExpressionStatementContext context)
        {
            base.VisitExpressionStatement(context);

            if (context.expression() != null)
            {
                return VisitExpression(context.expression());
            }

            return null;
        }

        public override CodeObject VisitAssignmentExpression(VclParser.AssignmentExpressionContext context)
        {
            base.VisitAssignmentExpression(context);

            var lhs = (CodeExpression)VisitUnaryExpression(context.lhs);
            var rhs = (CodeExpression)VisitExpression(context.rhs);

            if (lhs.GetExpressionType() != rhs.GetExpressionType())
            {
                rhs = ConvertExpression(rhs, lhs.GetExpressionType());
            }

            switch (context.op.GetText())
            {
                case "=":
                    return new CodeAssignStatement(lhs, rhs);

                case "+=":
                    return new CodeAssignStatement(lhs, new CodeBinaryOperatorExpression(lhs, CodeBinaryOperatorType.Add, rhs));

                case "-=":
                    return new CodeAssignStatement(lhs, new CodeBinaryOperatorExpression(lhs, CodeBinaryOperatorType.Subtract, rhs));

                default:
                    throw new InvalidOperationException("Unexpected assignment operator encountered");
            }
        }

        public override CodeObject VisitConditionalExpression(VclParser.ConditionalExpressionContext context)
        {
            base.VisitConditionalExpression(context);

            throw new NotSupportedException();

            // TODO: Need to generate three methods (condition, true and false methods) then inject a snippet expression that makes the call

            //var result = VisitConditionalOrExpression(context.conditionalOrExpression());
            //if (context.ifTrue != null && context.ifFalse != null)
            //{
            //    return new CodeExpression() (
            //            (CodeExpression)VisitExclusiveOrExpression(context.lhs),
            //            CodeBinaryOperatorType.BitwiseOr,
            //            (CodeExpression)VisitExclusiveOrExpression(context.rhs))
            //        .SetExpressionType(typeof(int));
            //    result = Expression.Condition(
            //        result,
            //        VisitExpression(context.ifTrue),
            //        VisitExpression(context.ifFalse));
            //}

            //return result;
        }

        public override CodeObject VisitConditionalOrExpression(VclParser.ConditionalOrExpressionContext context)
        {
            base.VisitConditionalOrExpression(context);

            return new CodeBinaryOperatorExpression(
                    (CodeExpression)VisitConditionalAndExpression(context.lhs),
                    CodeBinaryOperatorType.BooleanOr,
                    (CodeExpression)VisitConditionalAndExpression(context.rhs))
                .SetExpressionType(typeof(bool));
        }

        public override CodeObject VisitConditionalAndExpression(VclParser.ConditionalAndExpressionContext context)
        {
            base.VisitConditionalAndExpression(context);

            return new CodeBinaryOperatorExpression(
                    (CodeExpression)VisitInclusiveOrExpression(context.lhs),
                    CodeBinaryOperatorType.BooleanAnd,
                    (CodeExpression)VisitInclusiveOrExpression(context.rhs))
                .SetExpressionType(typeof(bool));
        }

        public override CodeObject VisitInclusiveOrExpression(VclParser.InclusiveOrExpressionContext context)
        {
            base.VisitInclusiveOrExpression(context);

            return new CodeBinaryOperatorExpression(
                    (CodeExpression)VisitExclusiveOrExpression(context.lhs),
                    CodeBinaryOperatorType.BitwiseOr,
                    (CodeExpression)VisitExclusiveOrExpression(context.rhs))
                .SetExpressionType(typeof(int));
        }

        public override CodeObject VisitExclusiveOrExpression(VclParser.ExclusiveOrExpressionContext context)
        {
            base.VisitExclusiveOrExpression(context);

            // TODO: Need to find fix for missing XOR operator
            // TODO: Create function for LHS and RHS then inject code snippet expression for XOR
            throw new NotSupportedException();
            //return new CodeBinaryOperatorExpression(
            //        (CodeExpression)VisitAndExpression(context.lhs),
            //        CodeBinaryOperatorType.ExclusiveOr,
            //        (CodeExpression)VisitAndExpression(context.rhs))
            //    .SetExpressionType(typeof(int));
        }

        public override CodeObject VisitAndExpression(VclParser.AndExpressionContext context)
        {
            base.VisitAndExpression(context);

            return new CodeBinaryOperatorExpression(
                    (CodeExpression)VisitEqualityExpression(context.lhs),
                    CodeBinaryOperatorType.BitwiseAnd,
                    (CodeExpression)VisitEqualityExpression(context.rhs))
                .SetExpressionType(typeof(int));
        }

        public override CodeObject VisitEqualStandardExpression(VclParser.EqualStandardExpressionContext context)
        {
            base.VisitEqualStandardExpression(context);

            switch (context.op.Text)
            {
                case "==":
                    return new CodeBinaryOperatorExpression(
                            (CodeExpression)VisitRelationalExpression(context.lhs),
                            CodeBinaryOperatorType.IdentityEquality,
                            (CodeExpression)VisitRelationalExpression(context.rhs))
                        .SetExpressionType(typeof(bool));

                case "!=":
                    return new CodeBinaryOperatorExpression(
                            (CodeExpression)VisitRelationalExpression(context.lhs),
                            CodeBinaryOperatorType.IdentityInequality,
                            (CodeExpression)VisitRelationalExpression(context.rhs))
                        .SetExpressionType(typeof(bool));

                default:
                    throw new ArgumentException("Unknown equality operator encountered");
            }
        }

        public override CodeObject VisitMatchRegexExpression(VclParser.MatchRegexExpressionContext context)
        {
            base.VisitMatchRegexExpression(context);

            // Get regular expression from RHS context
            var regexExpression = (CodeExpression)VisitRegularExpression(context.rhs);

            var lhs = (CodeExpression)VisitRelationalExpression(context.lhs);

            // LHS expression needs to be string
            if (lhs.GetExpressionType() != typeof(string))
            {
                lhs = new CodeMethodInvokeExpression(
                        lhs, nameof(ToString))
                    .SetExpressionType(typeof(string));
            }

            // Build call to VclAcl.IsMatch method
            var result = ((CodeExpression)
                new CodeMethodInvokeExpression(
                    regexExpression,
                    nameof(Regex.IsMatch),
                    lhs))
                .SetExpressionType(typeof(bool));

            // Handle negation of the result as necessary
            if (context.op.Text == "!~")
            {
                result = new CodeBinaryOperatorExpression(
                        result,
                        CodeBinaryOperatorType.IdentityEquality,
                        new CodePrimitiveExpression(false))
                    .SetExpressionType(typeof(bool));
            }

            return result;
        }

        public override CodeObject VisitRegularExpression(VclParser.RegularExpressionContext context)
        {
            base.VisitRegularExpression(context);

            var expr = context.StringConstant().GetText().Trim('"');

            // TODO: Convert from PRCE regex to .NET regex

            // For now you'll need to do this by hand
            return new CodeObjectCreateExpression(
                typeof(Regex),
                new CodePrimitiveExpression(expr),
                new CodePrimitiveExpression(
                    RegexOptions.Singleline |
                    RegexOptions.Compiled |
                    RegexOptions.IgnoreCase))
                .SetExpressionType(typeof(Regex));
        }

        public override CodeObject VisitMatchAclExpression(VclParser.MatchAclExpressionContext context)
        {
            base.VisitMatchAclExpression(context);

            var lhs = (CodeExpression)VisitRelationalExpression(context.lhs);

            // Add code to convert LHS string into IPAddress
            if (lhs.GetExpressionType() == typeof(string))
            {
                lhs = new CodeMethodInvokeExpression(
                    new CodeTypeReferenceExpression(typeof(IPAddress)),
                    nameof(IPAddress.Parse),
                    lhs)
                    .SetExpressionType(typeof(IPAddress));
            }

            // Throw if LHS type is not an IPAddress
            if (lhs.GetExpressionType() != typeof(IPAddress))
            {
                throw new ArgumentException("Left expression must resolve to string or IPAddress");
            }

            // Build call to VclAcl.IsMatch method
            var result = ((CodeExpression)
                new CodeMethodInvokeExpression(
                    CompilerContext.AclReferences[context.rhs.GetText()],
                    nameof(VclAcl.IsMatch),
                    lhs))
                .SetExpressionType(typeof(bool));

            // Handle negation of the result as necessary
            if (context.op.Text == "!~")
            {
                result = new CodeBinaryOperatorExpression(
                        result,
                        CodeBinaryOperatorType.IdentityEquality,
                        new CodePrimitiveExpression(false))
                    .SetExpressionType(typeof(bool));
            }

            return result;
        }

        public override CodeObject VisitRelationalExpression(VclParser.RelationalExpressionContext context)
        {
            base.VisitRelationalExpression(context);

            switch (context.op.Text)
            {
                case "<":
                    return new CodeBinaryOperatorExpression(
                            (CodeExpression)VisitAdditiveExpression(context.lhs),
                            CodeBinaryOperatorType.LessThan,
                            (CodeExpression)VisitAdditiveExpression(context.rhs))
                        .SetExpressionType(typeof(bool));

                case ">":
                    return new CodeBinaryOperatorExpression(
                            (CodeExpression)VisitAdditiveExpression(context.lhs),
                            CodeBinaryOperatorType.GreaterThan,
                            (CodeExpression)VisitAdditiveExpression(context.rhs))
                        .SetExpressionType(typeof(bool));

                case "<=":
                    return new CodeBinaryOperatorExpression(
                            (CodeExpression)VisitAdditiveExpression(context.lhs),
                            CodeBinaryOperatorType.LessThanOrEqual,
                            (CodeExpression)VisitAdditiveExpression(context.rhs))
                        .SetExpressionType(typeof(bool));

                case ">=":
                    return new CodeBinaryOperatorExpression(
                            (CodeExpression)VisitAdditiveExpression(context.lhs),
                            CodeBinaryOperatorType.GreaterThanOrEqual,
                            (CodeExpression)VisitAdditiveExpression(context.rhs))
                        .SetExpressionType(typeof(bool));

                default:
                    throw new ArgumentException("Unknown relational operator encountered");
            }
        }

        public override CodeObject VisitAdditiveExpression(VclParser.AdditiveExpressionContext context)
        {
            base.VisitAdditiveExpression(context);

            switch (context.op.Text)
            {
                case "+":
                    return new CodeBinaryOperatorExpression(
                            (CodeExpression)VisitMultiplicativeExpression(context.lhs),
                            CodeBinaryOperatorType.Multiply,
                            (CodeExpression)VisitMultiplicativeExpression(context.rhs))
                        .SetExpressionType(typeof(int)); // TODO: Need to resolve this correctly

                case "-":
                    return new CodeBinaryOperatorExpression(
                            (CodeExpression)VisitMultiplicativeExpression(context.lhs),
                            CodeBinaryOperatorType.Multiply,
                            (CodeExpression)VisitMultiplicativeExpression(context.rhs))
                        .SetExpressionType(typeof(int)); // TODO: Need to resolve this correctly

                default:
                    throw new ArgumentException("Unknown relational operator encountered");
            }
        }

        public override CodeObject VisitMultiplicativeExpression(VclParser.MultiplicativeExpressionContext context)
        {
            base.VisitMultiplicativeExpression(context);

            // TODO: This is only valid for numeric types (int and double)

            switch (context.op.Text)
            {
                case "*":
                    return new CodeBinaryOperatorExpression(
                            (CodeExpression)VisitUnaryExpression(context.lhs),
                            CodeBinaryOperatorType.Multiply,
                            (CodeExpression)VisitUnaryExpression(context.rhs))
                        .SetExpressionType(typeof(int)); // TODO: Need to resolve this correctly

                case "/":
                    return new CodeBinaryOperatorExpression(
                            (CodeExpression)VisitUnaryExpression(context.lhs),
                            CodeBinaryOperatorType.Divide,
                            (CodeExpression)VisitUnaryExpression(context.rhs))
                        .SetExpressionType(typeof(int)); // TODO: Need to resolve this correctly

                case "%":
                    return new CodeBinaryOperatorExpression(
                            (CodeExpression)VisitUnaryExpression(context.lhs),
                            CodeBinaryOperatorType.Modulus,
                            (CodeExpression)VisitUnaryExpression(context.rhs))
                        .SetExpressionType(typeof(int)); // TODO: Need to resolve this correctly

                default:
                    throw new ArgumentException("Unknown relational operator encountered");
            }
        }

        public override CodeObject VisitUnaryNegateExpression(VclParser.UnaryNegateExpressionContext context)
        {
            base.VisitUnaryNegateExpression(context);

            return new CodeBinaryOperatorExpression(
                (CodeExpression)VisitUnaryExpression(context.unaryExpression()),
                CodeBinaryOperatorType.IdentityEquality,
                new CodePrimitiveExpression(false)).SetExpressionType(typeof(bool));
        }

        public override CodeObject VisitAccessMemberHttp(VclParser.AccessMemberHttpContext context)
        {
            if (!_contextObjectMapper.TryGetExpression(
                new CodeArgumentReferenceExpression("context"),
                context.obj.GetText(),
                "http",
                out var expression))
            {
                throw new ArgumentException("Unable to determine top-level object");
            }

            return new CodeIndexerExpression(
                    expression,
                    new CodePrimitiveExpression(context.header))
                .SetExpressionType(typeof(string));
        }

        public override CodeObject VisitAccessMemberNormal(VclParser.AccessMemberNormalContext context)
        {
            if (!_contextObjectMapper.TryGetExpression(
                new CodeArgumentReferenceExpression("context"),
                context.obj.GetText(),
                context.name.Text,
                out var expression))
            {
                throw new ArgumentException("Unable to determine top-level object");
            }

            // ReSharper disable once PossibleNullReferenceException
            var type = expression
                .GetExpressionType()
                .GetProperty(
                    context.name.Text,
                    BindingFlags.Public |
                    BindingFlags.IgnoreCase)
                .PropertyType;
            return new CodePropertyReferenceExpression(
                    expression, context.name.Text)
                .SetExpressionType(type);
        }

        private CodeExpression ConvertExpression(CodeExpression expr, Type targetType)
        {
            // Converting to string is easy...
            if (targetType == typeof(string))
            {
                return new CodeMethodInvokeExpression(expr, "ToString").SetExpressionType(typeof(string));
            }

            if (targetType == typeof(bool))
            {
                if (expr.GetExpressionType() == typeof(int))
                {
                    // Zero is falsey
                    return new CodeBinaryOperatorExpression(
                            expr,
                            CodeBinaryOperatorType.IdentityInequality,
                            new CodePrimitiveExpression(0))
                        .SetExpressionType(typeof(bool));
                }

                if (expr.GetExpressionType() == typeof(string))
                {
                    // Empty strings are falsey
                    return new CodeBinaryOperatorExpression(
                        new CodeMethodInvokeExpression(
                            new CodeTypeReferenceExpression(typeof(string)),
                            nameof(string.IsNullOrWhiteSpace),
                            expr),
                        CodeBinaryOperatorType.IdentityEquality,
                        new CodePrimitiveExpression(false));
                }
            }

            throw new NotSupportedException("Invalid cast exception");
        }

        private string GetUniqueLocalVariableName()
        {
            return $"anonObject{++_unnamedVariableIndex}";
        }
    }
}