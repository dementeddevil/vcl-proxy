using System;
using System.Collections.Generic;

namespace Im.Proxy.VclCore.UnitTests
{
    public class VclTestVisitor : VclBaseVisitor<bool>
    {
        public IList<string> Operations { get; } = new List<string>();

        public override bool VisitAssignmentExpression(VclParser.AssignmentExpressionContext context)
        {
            return base.VisitAssignmentExpression(context);
        }

        public override bool VisitAssignmentOperator(VclParser.AssignmentOperatorContext context)
        {
            return base.VisitAssignmentOperator(context);
        }

        public override bool VisitBackendDeclaration(VclParser.BackendDeclarationContext context)
        {
            return base.VisitBackendDeclaration(context);
        }

        public override bool VisitBackendElement(VclParser.BackendElementContext context)
        {
            return base.VisitBackendElement(context);
        }

        public override bool VisitBackendElementList(VclParser.BackendElementListContext context)
        {
            return base.VisitBackendElementList(context);
        }

        public override bool VisitBackendReferenceExpression(VclParser.BackendReferenceExpressionContext context)
        {
            return base.VisitBackendReferenceExpression(context);
        }

        public override bool VisitBlockItem(VclParser.BlockItemContext context)
        {
            return base.VisitBlockItem(context);
        }

        public override bool VisitBlockItemList(VclParser.BlockItemListContext context)
        {
            return base.VisitBlockItemList(context);
        }

        public override bool VisitCastExpression(VclParser.CastExpressionContext context)
        {
            return base.VisitCastExpression(context);
        }

        public override bool VisitCompileUnit(VclParser.CompileUnitContext context)
        {
            return base.VisitCompileUnit(context);
        }

        public override bool VisitCompoundStatement(VclParser.CompoundStatementContext context)
        {
            using (BeginScopedOperation("compound statement"))
            {
                return base.VisitCompoundStatement(context);
            }
        }

        public override bool VisitConditionalExpression(VclParser.ConditionalExpressionContext context)
        {
            return base.VisitConditionalExpression(context);
        }

        public override bool VisitConstantExpression(VclParser.ConstantExpressionContext context)
        {
            return base.VisitConstantExpression(context);
        }

        public override bool VisitDeclaration(VclParser.DeclarationContext context)
        {
            return base.VisitDeclaration(context);
        }

        public override bool VisitDottedExpression(VclParser.DottedExpressionContext context)
        {
            return base.VisitDottedExpression(context);
        }

        public override bool VisitEqualityExpression(VclParser.EqualityExpressionContext context)
        {
            return base.VisitEqualityExpression(context);
        }

        public override bool VisitErrorStatement(VclParser.ErrorStatementContext context)
        {
            return base.VisitErrorStatement(context);
        }

        public override bool VisitExpression(VclParser.ExpressionContext context)
        {
            return base.VisitExpression(context);
        }

        public override bool VisitExpressionStatement(VclParser.ExpressionStatementContext context)
        {
            return base.VisitExpressionStatement(context);
        }

        public override bool VisitFunctionDeclaration(VclParser.FunctionDeclarationContext context)
        {
            Operations.Add($"Function {context.children[1].GetText()}");
            return base.VisitFunctionDeclaration(context);
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

        public override bool VisitLogicalAndExpression(VclParser.LogicalAndExpressionContext context)
        {
            return base.VisitLogicalAndExpression(context);
        }

        public override bool VisitLogicalOrExpression(VclParser.LogicalOrExpressionContext context)
        {
            return base.VisitLogicalOrExpression(context);
        }

        public override bool VisitMatchExpression(VclParser.MatchExpressionContext context)
        {
            return base.VisitMatchExpression(context);
        }

        public override bool VisitPrimaryExpression(VclParser.PrimaryExpressionContext context)
        {
            return base.VisitPrimaryExpression(context);
        }

        public override bool VisitRegularExpression(VclParser.RegularExpressionContext context)
        {
            return base.VisitRegularExpression(context);
        }

        public override bool VisitRemoveStatement(VclParser.RemoveStatementContext context)
        {
            return base.VisitRemoveStatement(context);
        }

        public override bool VisitReturnStatement(VclParser.ReturnStatementContext context)
        {
            Operations.Add($"Return {context.children[2].GetText()}");
            return base.VisitReturnStatement(context);
        }

        public override bool VisitSetStatement(VclParser.SetStatementContext context)
        {
            return base.VisitSetStatement(context);
        }

        public override bool VisitStatement(VclParser.StatementContext context)
        {
            return base.VisitStatement(context);
        }

        public override bool VisitSyntheticStatement(VclParser.SyntheticStatementContext context)
        {
            return base.VisitSyntheticStatement(context);
        }

        public override bool VisitTranslationUnit(VclParser.TranslationUnitContext context)
        {
            return base.VisitTranslationUnit(context);
        }

        public override bool VisitUnaryExpression(VclParser.UnaryExpressionContext context)
        {
            return base.VisitUnaryExpression(context);
        }

        public override bool VisitUnaryOperator(VclParser.UnaryOperatorContext context)
        {
            return base.VisitUnaryOperator(context);
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
