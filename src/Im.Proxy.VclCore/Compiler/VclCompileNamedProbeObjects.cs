using System;
using System.CodeDom;
using Im.Proxy.VclCore.Model;

namespace Im.Proxy.VclCore.Compiler
{
    /// <summary>
    /// Builds a dictionary of expressions that can create VclProbe objects that
    /// have been described in the VCL as named entities
    /// </summary>
    /// <remarks>
    /// This must be run prior to the main compile (which needs to refer to these probes)
    /// </remarks>
    /// <seealso cref="VclBaseExpressionVisitor" />
    public class VclCompileNamedProbeObjects : VclBaseExpressionVisitor
    {
        public VclCompileNamedProbeObjects(VclCompilerContext compilerContext)
            : base(compilerContext)
        {
        }

        protected CodeExpression CurrentProbeExpression { get; set; }

        public override CodeObject VisitProbeDeclaration(VclParser.ProbeDeclarationContext context)
        {
            var name = context.Identifier().GetText();
            if (CompilerContext.ProbeReferences.ContainsKey(name))
            {
                throw new ArgumentException("Probe name is not unique");
            }

            // Determine field name
            var fieldName = name.SafeIdentifier("_probe");

            // Setup current probe object and add to probe mapper
            CurrentProbeExpression = new CodeFieldReferenceExpression(
                new CodeThisReferenceExpression(), fieldName);
            CompilerContext.ProbeReferences.Add(name, (CodeFieldReferenceExpression)CurrentProbeExpression);

            // Create field and add to handler class
            CompilerContext.HandlerClass.Members.Add(
                new CodeMemberField(typeof(VclProbe), fieldName)
                {
                    Attributes = MemberAttributes.Private,
                    InitExpression =
                        new CodeObjectCreateExpression(
                            typeof(VclProbe),
                            new CodePrimitiveExpression(name))
                });

            base.VisitProbeDeclaration(context);

            CurrentProbeExpression = null;
            return null;
        }

        public override CodeObject VisitProbeStringVariableExpression(VclParser.ProbeStringVariableExpressionContext context)
        {
            base.VisitProbeStringVariableExpression(context);

            var normalisedMemberName = context.name.GetText().Replace("_", "");
            CompilerContext.InitStatements.Add(
                new CodeAssignStatement(
                    new CodePropertyReferenceExpression(
                        CurrentProbeExpression,
                        normalisedMemberName),
                    (CodeExpression)VisitStringLiteral(context.stringLiteral())));

            return null;
        }

        public override CodeObject VisitProbeIntegerVariableExpression(VclParser.ProbeIntegerVariableExpressionContext context)
        {
            base.VisitProbeIntegerVariableExpression(context);

            var normalisedMemberName = context.name.GetText().Replace("_", "");
            CompilerContext.InitStatements.Add(
                new CodeAssignStatement(
                    new CodePropertyReferenceExpression(
                        CurrentProbeExpression,
                        normalisedMemberName),
                    (CodeExpression)VisitIntegerLiteral(context.integerLiteral())));

            return null;
        }

        public override CodeObject VisitProbeTimeVariableExpression(VclParser.ProbeTimeVariableExpressionContext context)
        {
            base.VisitProbeTimeVariableExpression(context);

            var normalisedMemberName = context.name.GetText().Replace("_", "");
            CompilerContext.InitStatements.Add(
                new CodeAssignStatement(
                    new CodePropertyReferenceExpression(
                        CurrentProbeExpression,
                        normalisedMemberName),
                    (CodeExpression)VisitTimeLiteral(context.timeLiteral())));

            return null;
        }
    }
}