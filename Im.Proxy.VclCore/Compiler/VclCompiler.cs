using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using Antlr4.Runtime;
using Im.Proxy.VclCore.Model;
using Microsoft.AspNetCore.Http;
using MemberBinding = System.Linq.Expressions.MemberBinding;

namespace Im.Proxy.VclCore.Compiler
{
    public class VclCompiler
    {
        public TResult CompileAndVisit<TResult>(string vclTextFile, IVclVisitor<TResult> visitor)
        {
            using (var textStream = new StringReader(vclTextFile))
            {
                // Pass text stream through lexer for tokenising
                var tokenStream = new AntlrInputStream(textStream);
                var lexer = new VclLexer(tokenStream);

                // Pass token stream through parser to product AST
                var stream = new CommonTokenStream(lexer);
                var parser = new VclParser(stream);

                return visitor.Visit(parser.compileUnit());
            }
        }
    }

    /// <summary>
    /// Builds a dictionary of expressions that can create VclProbe objects that
    /// have been described in the VCL as named entities
    /// </summary>
    /// <remarks>
    /// This must be run prior to the main compile (which needs to refer to these probes)
    /// </remarks>
    /// <seealso cref="Im.Proxy.VclCore.VclBaseVisitor{System.Linq.Expressions.Expression}" />
    public class VclCompileNamedProbeObjects : VclBaseVisitor<Expression>
    {
        private IList<MemberBinding> CurrentProbeBindings { get; } = new List<MemberBinding>();

        public IDictionary<string, Expression> ProbeExpressions { get; } =
            new Dictionary<string, Expression>(StringComparer.OrdinalIgnoreCase);

        public override Expression VisitProbeDeclaration(VclParser.ProbeDeclarationContext context)
        {
            var name = context.Identifier().GetText();
            if (ProbeExpressions.ContainsKey(name))
            {
                throw new ArgumentException("Probe name is not unique");
            }

            // TODO: Setup current probe expression
            CurrentProbeBindings.Clear();

            base.VisitProbeDeclaration(context);

            var probeTypeCtor = typeof(VclProbe).GetConstructor(new[] { typeof(string) });
            ProbeExpressions.Add(
                name,
                Expression.MemberInit(
                    Expression.New(probeTypeCtor, Expression.Constant(name)),
                    CurrentProbeBindings));

            return null;
        }

        public override Expression VisitProbeStringVariableExpression(VclParser.ProbeStringVariableExpressionContext context)
        {
            base.VisitProbeStringVariableExpression(context);

            var normalisedMemberName = context.name.GetText().Replace("_", "");

            var propInfo = typeof(VclProbe).GetProperty(
                normalisedMemberName,
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.IgnoreCase |
                BindingFlags.SetProperty);
            CurrentProbeBindings.Add(
                Expression.Bind(
                    propInfo,
                    VisitStringLiteral(context.stringLiteral())));

            return null;
        }

        public override Expression VisitProbeIntegerVariableExpression(VclParser.ProbeIntegerVariableExpressionContext context)
        {
            base.VisitProbeIntegerVariableExpression(context);

            var normalisedMemberName = context.name.GetText().Replace("_", "");
            var propInfo = typeof(VclProbe).GetProperty(
                normalisedMemberName,
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.IgnoreCase |
                BindingFlags.SetProperty);
            CurrentProbeBindings.Add(
                Expression.Bind(
                    propInfo,
                    VisitIntegerLiteral(context.integerLiteral())));

            return null;
        }

        public override Expression VisitProbeTimeVariableExpression(VclParser.ProbeTimeVariableExpressionContext context)
        {
            base.VisitProbeTimeVariableExpression(context);

            var normalisedMemberName = context.name.GetText().Replace("_", "");
            var propInfo = typeof(VclProbe).GetProperty(
                normalisedMemberName,
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.IgnoreCase |
                BindingFlags.SetProperty);
            CurrentProbeBindings.Add(
                Expression.Bind(
                    propInfo,
                    VisitTimeLiteral(context.timeLiteral())));

            return null;
        }

        public override Expression VisitStringLiteral(VclParser.StringLiteralContext context)
        {
            return Expression.Constant(context.StringConstant().GetText());
        }

        public override Expression VisitIntegerLiteral(VclParser.IntegerLiteralContext context)
        {
            return Expression.Constant(int.Parse(context.IntegerConstant().GetText()));
        }

        public override Expression VisitTimeLiteral(VclParser.TimeLiteralContext context)
        {
            var rawValue = context.TimeConstant().GetText();
            var value = TimeSpan.Zero;
            if (rawValue.EndsWith("ms"))
            {
                var timeComponentText = rawValue.Substring(0, rawValue.Length - 2);
                value = TimeSpan.FromMilliseconds(int.Parse(timeComponentText));
            }
            else
            {
                var timeComponentText = rawValue.Substring(0, rawValue.Length - 1);
                switch (rawValue.Substring(rawValue.Length - 1, 1).ToLower())
                {
                    case "s":
                        value = TimeSpan.FromMilliseconds(int.Parse(timeComponentText));
                        break;
                    case "m":
                        value = TimeSpan.FromMilliseconds(int.Parse(timeComponentText));
                        break;
                    case "d":
                        value = TimeSpan.FromMilliseconds(int.Parse(timeComponentText));
                        break;
                    case "w":
                        value = TimeSpan.FromMilliseconds(int.Parse(timeComponentText));
                        break;
                    case "y":
                        value = TimeSpan.FromMilliseconds(int.Parse(timeComponentText));
                        break;
                    default:
                        throw new InvalidOperationException("Unable to parse time component");
                }
            }

            return Expression.Constant(value);
        }
    }

    public class VclHandlerCompiler : VclBaseVisitor<Expression>
    {
        private IList<Expression> _vclInitExpressions = new List<Expression>();

        public VclHandlerCompiler(VclHandlerCompilerContext context)
        {
            var assemblyName = new AssemblyName("NameOfVclFile");
            var assemblyBuilder = AssemblyBuilder
                .DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.RunAndSave);
            var assemblyModule = assemblyBuilder.DefineDynamicModule("MyDynamicModule");
            var handlerTypeBuilder = assemblyModule
                .DefineType(
                    "VclHandlerImpl",
                    TypeAttributes.Public | TypeAttributes.Class,
                    typeof(VclHandler));

            // Hoist into wrapper class for tracking subroutine content
            var vclInitMethodBuilder = handlerTypeBuilder
                .DefineMethod(
                    "VclInit",
                    MethodAttributes.Family,
                    typeof(void),
                    new[]
                    {
                        typeof(VclContext)
                    });
            handlerTypeBuilder.DefineMethodOverride(
                vclInitMethodBuilder,
                typeof(VclHandler).GetMethod("VclInit", BindingFlags.Instance | BindingFlags.NonPublic));

            var vclInitCompoundStatement = Expression.Block(_vclInitExpressions);
            //Expression<Action> vclInitExpr= Expression.Lambda<Action>(vclInitCompoundStatement,); 
            //vclInitExpr.C vclInitCompoundStatement.
        }

        public override Expression VisitBackendDeclaration(VclParser.BackendDeclarationContext context)
        {
            return base.VisitBackendDeclaration(context);
        }
    }

    public class VclHandlerCompilerContext
    {
        public IList<VclBackend> Backends { get; } = new List<VclBackend>();

        public IList<VclProbe> Probes { get; } = new List<VclProbe>();

        //public IList<VclAcl> Acls { get; } = new List<VclAcl>();
    }
}
