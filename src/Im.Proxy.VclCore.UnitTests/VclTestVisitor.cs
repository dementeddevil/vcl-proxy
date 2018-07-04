using System;
using System.Collections.Generic;

namespace Im.Proxy.VclCore.UnitTests
{
    public class VclTestVisitor : VclBaseVisitor<bool>
    {
        public IList<string> Operations { get; } = new List<string>();

        public override bool VisitCompoundStatement(VclParser.CompoundStatementContext context)
        {
            using (BeginScopedOperation("compound statement"))
            {
                return base.VisitCompoundStatement(context);
            }
        }

        public override bool VisitProcedureDeclaration(VclParser.ProcedureDeclarationContext context)
        {
            Operations.Add($"Function {context.children[1].GetText()}");
            return base.VisitProcedureDeclaration(context);
        }

        public override bool VisitIfStatement(VclParser.IfStatementContext context)
        {
            Operations.Add($"If {context.children[2].GetText()}");
            return base.VisitIfStatement(context);
        }

        public override bool VisitIncludeDeclaration(VclParser.IncludeDeclarationContext context)
        {
            Operations.Add($"Include {context.children[1].GetText().Trim('"')}");
            return base.VisitIncludeDeclaration(context);
        }

        public override bool VisitReturnStatement(VclParser.ReturnStatementContext context)
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
