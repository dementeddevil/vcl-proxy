using System;
using System.CodeDom;
using System.Collections.Generic;

namespace Im.Proxy.VclCore.Compiler
{
    public class VclCompilerContext
    {
        public VclCompilerContext()
        {
            HandlerClass = new CodeTypeDeclaration("VclCustomHandler");
            HandlerClass.BaseTypes.Add(typeof(VclHandler));
            HandlerClass.IsClass = true;
            HandlerClass.Comments.Add(
                new CodeCommentStatement(
                    @"<summary>
<c>VclCustomHandler</c> extends <see cref=""VclHandler"" /> by integrating
custom actions derived from parsing a custom VCL file.
</summary>
<remarks>
For further information about Varnish Configuration Language please head over
to: https://book.varnish-software.com/4.0/
</remarks>",
                    true));
        }

        public CodeTypeDeclaration HandlerClass { get; }

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
}