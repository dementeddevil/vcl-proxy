﻿using System.Collections.Generic;

namespace Im.Proxy.VclCore.Compiler
{
    public class VclCompileIncludes : VclLangBaseVisitor<bool>
    {
        public IList<string> Files { get; } = new List<string>();

        public override bool VisitIncludeDeclaration(VclLangParser.IncludeDeclarationContext context)
        {
            Files.Add(context.StringConstant().GetText().Trim('\"'));
            return base.VisitIncludeDeclaration(context);
        }
    }
}