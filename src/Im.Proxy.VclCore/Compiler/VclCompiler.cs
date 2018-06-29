using System;
using System.CodeDom;
using System.Collections.Generic;
using System.IO;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using Antlr4.Runtime;
using Im.Proxy.VclCore.Model;

namespace Im.Proxy.VclCore.Compiler
{
    public class VclCompiler
    {
        public void CompileAndBuildModule(string vclTextFile, string outputssemby)
        {
            var compilerResult = Compile(vclTextFile);

            var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(
                new AssemblyName(Path.GetFileNameWithoutExtension(outputssemby)),
                AssemblyBuilderAccess.Save);
            var moduleBuilder = assemblyBuilder.DefineDynamicModule("Im.VclCore");

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

            // TODO: Call through subroutine compiler
            // TODO: making use of previous copilation stagss
        }

        private string GetSafeName(string entityKind, string entityName) => $"_{entityKind}_{entityName.Replace('-', '_')}";

        public VclCompilerContext Compile(string vclTextFile)
        {
            // Compile named probe entries
            var compilerContext = new VclCompilerContext();
            var probeCompiler = new VclCompileNamedProbeObjects(compilerContext);
            CompileAndVisit(vclTextFile, probeCompiler);

            // Ensure we have a default probe in the named probe entries
            if (!compilerContext.ProbeReferences.ContainsKey("default"))
            {
                var fieldName = "default".SafeIdentifier("_probe");
                compilerContext.ProbeReferences.Add(
                    "default", new CodeFieldReferenceExpression(
                        new CodeThisReferenceExpression(), fieldName));

                // Create field and add to handler class
                compilerContext.HandlerClass.Members.Add(
                    new CodeMemberField(typeof(VclProbe), fieldName)
                    {
                        Attributes = MemberAttributes.Private,
                        InitExpression =
                            new CodeObjectCreateExpression(
                                typeof(VclProbe),
                                new CodePrimitiveExpression("default"))
                    });
            }

            // Compile named backend entries
            var backendCompiler = new VclCompileNamedBackendObjects(compilerContext);
            CompileAndVisit(vclTextFile, backendCompiler);

            // Compile named ACL entries
            var aclCompiler = new VclCompileNamedAclObjects(compilerContext);
            CompileAndVisit(vclTextFile, aclCompiler);

            // TODO: Compile include references into list (if we bother to implement)

            // TODO: Compile named director entries (if we bother to implement)

            // Compile subroutines entries
            var subCompiler = new VclCompileNamedSubroutine(compilerContext);
            CompileAndVisit(vclTextFile, subCompiler);

            return compilerContext;
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

    public class VclCompilerContext
    {
        public CodeTypeDeclaration HandlerClass { get; set; }

        public IDictionary<string, CodeFieldReferenceExpression> ProbeReferences { get; } =
            new Dictionary<string, CodeFieldReferenceExpression>(StringComparer.OrdinalIgnoreCase);

        public IDictionary<string, CodeFieldReferenceExpression> AclReferences { get; } =
            new Dictionary<string, CodeFieldReferenceExpression>(StringComparer.OrdinalIgnoreCase);

        public IDictionary<string, CodeFieldReferenceExpression> BackendReferences { get; } =
            new Dictionary<string, CodeFieldReferenceExpression>(StringComparer.OrdinalIgnoreCase);

        public IDictionary<string, CodeStatementCollection> MethodStatements { get; } =
            new Dictionary<string, CodeStatementCollection>(StringComparer.OrdinalIgnoreCase)
            {
                { "vcl_init", new CodeStatementCollection() }
            };

        public CodeStatementCollection InitStatements => MethodStatements["vcl_init"];
    }

    public class CompilerResult
    {
        public IDictionary<string, Expression> ProbeExressions { get; set; }

        public IDictionary<string, Expression> BackendExpressions { get; set; }

        public IDictionary<string, Expression> AclExpressions { get; set; }
    }
}
