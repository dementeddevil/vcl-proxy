using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using Im.Proxy.VclCore.Model;

namespace Im.Proxy.VclCore.Compiler
{
    /// <summary>
    /// Builds a dictionary of expressions that can create VclBackend objects that
    /// have been described in the VCL as named entities
    /// </summary>
    /// <remarks>
    /// This must be run prior to the main compile (which needs to refer to these probes)
    /// </remarks>
    /// <seealso cref="VclBaseExpressionVisitor" />
    public class VclCompileNamedBackendObjects : VclCompileNamedProbeObjects
    {
        public VclCompileNamedBackendObjects(VclCompilerContext compilerContext)
            : base(compilerContext)
        {
        }

        private CodeFieldReferenceExpression _currentBackendReference;

        public override CodeObject VisitBackendDeclaration(VclParser.BackendDeclarationContext context)
        {
            // Cache the current backend name
            var name = context.Identifier().GetText();
            if (CompilerContext.BackendReferences.ContainsKey(name))
            {
                throw new ArgumentException("Backend name is not unique");
            }

            // Determine field name
            var fieldName = name.SafeIdentifier("_backend");

            // Setup current probe object and add to probe mapper
            CompilerContext.BackendReferences.Add(
                name,
                _currentBackendReference =
                    new CodeFieldReferenceExpression(
                        new CodeThisReferenceExpression(), fieldName));

            // Create field and add to handler class
            CompilerContext.HandlerClass.Members.Add(
                new CodeMemberField(typeof(VclBackend), fieldName)
                {
                    Attributes = MemberAttributes.Private,
                    InitExpression =
                        new CodeObjectCreateExpression(
                            typeof(VclBackend),
                            new CodePrimitiveExpression(name))
                });

            // Setup probe reference to member variable (used when dealing with inline probe expressions)
            CurrentProbeExpression =
                new CodePropertyReferenceExpression(
                    _currentBackendReference,
                    nameof(VclBackend.Probe));

            base.VisitBackendDeclaration(context);

            _currentBackendReference = null;
            return null;
        }

        public override CodeObject VisitBackendStringVariableExpression(VclParser.BackendStringVariableExpressionContext context)
        {
            base.VisitBackendStringVariableExpression(context);

            var normalisedMemberName = context.name.GetText().Replace("_", "");
            CompilerContext.InitStatements.Add(
                new CodeAssignStatement(
                    new CodePropertyReferenceExpression(
                        _currentBackendReference,
                        normalisedMemberName),
                    (CodeExpression)VisitStringLiteral(context.stringLiteral())));

            return null;
        }

        public override CodeObject VisitBackendIntegerVariableExpression(VclParser.BackendIntegerVariableExpressionContext context)
        {
            base.VisitBackendIntegerVariableExpression(context);

            var normalisedMemberName = context.name.GetText().Replace("_", "");
            CompilerContext.InitStatements.Add(
                new CodeAssignStatement(
                    new CodePropertyReferenceExpression(
                        _currentBackendReference,
                        normalisedMemberName),
                    (CodeExpression)VisitIntegerLiteral(context.integerLiteral())));

            return null;
        }

        public override CodeObject VisitBackendTimeVariableExpression(VclParser.BackendTimeVariableExpressionContext context)
        {
            base.VisitBackendTimeVariableExpression(context);

            var normalisedMemberName = context.name.GetText().Replace("_", "");
            CompilerContext.InitStatements.Add(
                new CodeAssignStatement(
                    new CodePropertyReferenceExpression(
                        _currentBackendReference,
                        normalisedMemberName),
                    (CodeExpression)VisitTimeLiteral(context.timeLiteral())));

            return null;
        }

        public override CodeObject VisitBackendProbeVariableExpression(VclParser.BackendProbeVariableExpressionContext context)
        {
            base.VisitBackendProbeVariableExpression(context);

            var normalisedMemberName = context.name.Text.Replace("_", "");
            CompilerContext.InitStatements.Add(
                new CodeAssignStatement(
                    new CodePropertyReferenceExpression(
                        CurrentProbeExpression,
                        normalisedMemberName),
                    (CodeExpression)VisitProbeExpression(context.probeExpression())));

            return null;
        }

        public override CodeObject VisitProbeDeclaration(VclParser.ProbeDeclarationContext context)
        {
            // Named probe declarations are already handled elsewhere
            return null;
        }

        public override CodeObject VisitProbeExpression(VclParser.ProbeExpressionContext context)
        {
            if (context.probeReferenceExpression() != null)
            {
                return VisitProbeReferenceExpression(context.probeReferenceExpression());
            }

            if (context.probeInlineExpression() != null)
            {
                return VisitProbeInlineExpression(context.probeInlineExpression());
            }

            return null;
        }

        public override CodeObject VisitProbeReferenceExpression(VclParser.ProbeReferenceExpressionContext context)
        {
            var probeName = context.probeName.Text;
            if (!CompilerContext.ProbeReferences.ContainsKey(probeName))
            {
                throw new ArgumentException($"Named probe ({probeName}) not found");
            }

            return CompilerContext.ProbeReferences[probeName];
        }
    }
}