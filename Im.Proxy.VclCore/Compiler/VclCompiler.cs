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
