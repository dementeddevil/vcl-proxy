using System.IO;
using System.Linq.Expressions;
using Antlr4.Runtime;
using Im.Proxy.VclCore.Model;

namespace Im.Proxy.VclCore.Compiler
{
    public class VclCompiler
    {
        public void Compile(string vclTextFile)
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

            // TODO: Compile named ACL entries

            // TODO: Compile named director entries (if we bother to implement)

            // TODO: Compile subroutines into derived handler

            // TODO: Return built assembly
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
}
