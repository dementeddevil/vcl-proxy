using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
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
        // Compile to CodeDOM rather than expression tree due to needing derived class
        //  we'll be able to save assemblies then

        public VclHandlerCompiler(VclHandlerCompilerContext context)
        {
            Expression.

        }
    }

    public class VclHandlerCompilerContext
    {
        public IList<VclBackend> Backends { get; } = new List<VclBackend>();

        public IList<VclProbe> Probes { get; } = new List<VclProbe>();

        //public IList<VclAcl> Acls { get; } = new List<VclAcl>();
    }
}
