using System;
using System.Collections.Generic;
using System.IO;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using Antlr4.Runtime;
using Im.Proxy.VclCore.Model;

namespace Im.Proxy.VclCore.Compiler
{
    public class VclCompiler
    {
        public void CompileAndBuildModule(string vclTextFile)
        {
            var compilerResult = Compile(vclTextFile);

            var moduleBuilder = new ModuleBuilder();

            // Create derived handler class 
            var typeBuilder = moduleBuilder.DefineType(
                "DerivedVclHandler",
                TypeAttributes.Public |
                TypeAttributes.Sealed,
                typeof(VclHandler));

            // Walk the list of ACLs and create fields
            var initStatements = new List<Expression>();
            var thisExpression = Expression.Variable(typeBuilder, "owner");
            foreach (var entry in compilerResult.AclExpressions)
            {
                var fieldName = GetSafeName("acl", entry.Key);
                var fieldBuilder = typeBuilder.DefineField(
                    fieldName,
                    typeof(VclAcl),
                    FieldAttributes.Private);
                var fieldReference = Expression
                    .Field(thisExpression, fieldBuilder);
                initStatements.Add(Expression.Assign(fieldReference, entry.Value));
            }

            // Build vcl_init
            var initMethodBuilder = typeBuilder
                .DefineMethod(
                    "VclInit",
                    MethodAttributes.Family,
                    typeof(void),
                    new Type[0]);
            initMethodBuilder.SetMethodBody();
        }

        private string GetSafeName(string entityKind, string entityName) => $"_{entityKind}_{entityName.Replace('-', '_')}";

        public CompilerResult Compile(string vclTextFile)
        {
            // Compile named probe entries
            var probeCompiler = new VclCompileNamedProbeObjects();
            CompileAndVisit(vclTextFile, probeCompiler);

            // Ensure we have a default probe in the named probe entries
            if (!probeCompiler.ProbeExpressions.ContainsKey("default"))
            {
                var probeTypeCtor = typeof(VclProbe).GetConstructor(new[] { typeof(string) });
                probeCompiler.ProbeExpressions.Add(
                    "default",
                    Expression.New(probeTypeCtor, Expression.Constant("default")));
            }

            // Compile named backend entries
            var backendCompiler = new VclCompileNamedBackendObjects(
                probeCompiler.ProbeExpressions);
            CompileAndVisit(vclTextFile, backendCompiler);

            // Compile named ACL entries
            var aclCompiler = new VclCompileNamedAclObjects();
            CompileAndVisit(vclTextFile, aclCompiler);

            // TODO: Compile include references into list (if we bother to implement)

            // TODO: Compile named director entries (if we bother to implement)

            // TODO: Compile subroutines into derived handler

            return new CompilerResult
                   {
                       ProbeExressions = probeCompiler.ProbeExpressions,
                       BackendExpressions = backendCompiler.BackendExpressions,
                       AclExpressions = aclCompiler.AclExpressions
                   };
        }

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

    public class CompilerResult
    {
        public IDictionary<string, Expression> ProbeExressions { get; set; }

        public IDictionary<string, Expression> BackendExpressions { get; set; }

        public IDictionary<string, Expression> AclExpressions { get; set; }
    }
}
