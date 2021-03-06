﻿using System;
using System.Collections.Generic;
using Im.Proxy.VclCore.Compiler;

namespace Im.Proxy.VclCore.UnitTests
{
    public class VclTestVisitor : VclLangBaseVisitor<bool>
    {
        public IList<string> Operations { get; } = new List<string>();

        public override bool VisitCompoundStatement(VclLangParser.CompoundStatementContext context)
        {
            using (BeginScopedOperation("compound statement"))
            {
                return base.VisitCompoundStatement(context);
            }
        }

        public override bool VisitProcedureDeclaration(VclLangParser.ProcedureDeclarationContext context)
        {
            Operations.Add($"Function {context.children[1].GetText()}");
            return base.VisitProcedureDeclaration(context);
        }

        public override bool VisitIfStatement(VclLangParser.IfStatementContext context)
        {
            Operations.Add($"If {context.children[2].GetText()}");
            return base.VisitIfStatement(context);
        }

        public override bool VisitIncludeDeclaration(VclLangParser.IncludeDeclarationContext context)
        {
            Operations.Add($"Include {context.children[1].GetText().Trim('"')}");
            return base.VisitIncludeDeclaration(context);
        }

        public override bool VisitReturnStatement(VclLangParser.ReturnStatementContext context)
        {
            Operations.Add($"Return {context.children[2].GetText()}");
            return base.VisitReturnStatement(context);
        }

        private IDisposable BeginScopedOperation(string operation)
        {
            Operations.Add($"Enter {operation}");
            return new ScopedOperationHelper(
                () => Operations.Add($"Leave {operation}"));
        }

        private class ScopedOperationHelper : IDisposable
        {
            private Action _disposeAction;
            private bool _isDisposed;

            public ScopedOperationHelper(Action disposeAction)
            {
                _disposeAction = disposeAction;
            }

            public void Dispose()
            {
                if (!_isDisposed)
                {
                    _isDisposed = true;
                    _disposeAction();
                    _disposeAction = null;
                }
            }
        }
    }
}
