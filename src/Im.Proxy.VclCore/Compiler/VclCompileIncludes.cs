using System.Collections.Generic;

namespace Im.Proxy.VclCore.Compiler
{
    public class VclCompileIncludes : VclParserBaseVisitor<bool>
    {
        public IList<string> Files { get; } = new List<string>();

        public override bool VisitIncludeDeclaration(VclParser.IncludeDeclarationContext context)
        {
            Files.Add(context.StringConstant().GetText().Trim('\"'));
            return base.VisitIncludeDeclaration(context);
        }
    }
}