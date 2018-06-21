using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
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

    public class VclCompileNamedObjects : VclBaseVisitor<Expression>
    {
        private IList<MemberBinding> CurrentProbeBindings { get; }


        public IDictionary<string, Expression> ProbeExpressions { get; } =
            new Dictionary<string, Expression>(StringComparer.OrdinalIgnoreCase);

        public IDictionary<string, Expression> BackendExpressions { get; } =
            new Dictionary<string, Expression>(StringComparer.OrdinalIgnoreCase);


        public override Expression VisitBackendDeclaration(VclParser.BackendDeclarationContext context)
        {
            return base.VisitBackendDeclaration(context);
        }

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

        public override Expression VisitProbeElement(VclParser.ProbeElementContext context)
        {
            base.VisitProbeElement(context);

            //base.VisitPro
            var normalisedMemberName = context.probeVariableName().GetText().Replace("_", "");

            var propInfo = typeof(VclProbe).GetProperty(
                normalisedMemberName,
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.IgnoreCase);
            CurrentProbeBindings.Add(
                Expression.Bind(propInfo, Expression.Constant(null)));

            return null;
        }
    }

    public class VclHandlerCompiler : VclBaseVisitor<Expression>
    {
        public VclHandlerCompiler(VclHandlerCompilerContext context)
        {
            //Expression.

        }
    }

    public class VclHandlerCompilerContext
    {
        public IList<VclBackend> Backends { get; } = new List<VclBackend>();

        public IList<VclProbe> Probes { get; } = new List<VclProbe>();

        //public IList<VclAcl> Acls { get; } = new List<VclAcl>();
    }
}
