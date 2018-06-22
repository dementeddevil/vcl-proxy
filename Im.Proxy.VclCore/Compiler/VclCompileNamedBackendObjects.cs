using System;
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
        public VclCompileNamedBackendObjects(IDictionary<string, Expression> probeExpressions)
            : base(probeExpressions)
        {
        }

        public IDictionary<string, Expression> BackendExpressions { get; } =
            new Dictionary<string, Expression>(StringComparer.OrdinalIgnoreCase);

        private IList<MemberBinding> CurrentBackendBindings { get; } = new List<MemberBinding>();

        private string CurrentBackendName { get; set; }

        public override Expression VisitBackendDeclaration(VclParser.BackendDeclarationContext context)
        {
            // Cache the current backend name
            CurrentBackendName = context.Identifier().GetText();
            if (BackendExpressions.ContainsKey(CurrentBackendName))
            {
                throw new ArgumentException("Backend name is not unique");
            }

            CurrentBackendBindings.Clear();
            base.VisitBackendDeclaration(context);

            var name = context.Identifier().GetText();
            var backendTypeCtor = typeof(VclBackend).GetConstructor(new[] { typeof(string) });
            BackendExpressions.Add(
                name,
                Expression.MemberInit(
                    Expression.New(backendTypeCtor),
                    CurrentBackendBindings));

            return null;
        }

        public override Expression VisitBackendStringVariableExpression(VclParser.BackendStringVariableExpressionContext context)
        {
            base.VisitBackendStringVariableExpression(context);

            var normalisedMemberName = context.name.GetText().Replace("_", "");
            var propInfo = typeof(VclProbe).GetProperty(
                normalisedMemberName,
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.IgnoreCase |
                BindingFlags.SetProperty);
            CurrentBackendBindings.Add(
                Expression.Bind(
                    propInfo,
                    VisitStringLiteral(context.stringLiteral())));

            return null;
        }

        public override Expression VisitBackendIntegerVariableExpression(VclParser.BackendIntegerVariableExpressionContext context)
        {
            base.VisitBackendIntegerVariableExpression(context);

            var normalisedMemberName = context.name.GetText().Replace("_", "");
            var propInfo = typeof(VclProbe).GetProperty(
                normalisedMemberName,
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.IgnoreCase |
                BindingFlags.SetProperty);
            CurrentBackendBindings.Add(
                Expression.Bind(
                    propInfo,
                    VisitIntegerLiteral(context.integerLiteral())));

            return null;
        }

        public override Expression VisitBackendTimeVariableExpression(VclParser.BackendTimeVariableExpressionContext context)
        {
            base.VisitBackendTimeVariableExpression(context);

            var normalisedMemberName = context.name.GetText().Replace("_", "");
            var propInfo = typeof(VclProbe).GetProperty(
                normalisedMemberName,
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.IgnoreCase |
                BindingFlags.SetProperty);
            CurrentBackendBindings.Add(
                Expression.Bind(
                    propInfo,
                    VisitTimeLiteral(context.timeLiteral())));

            return null;
        }

        public override Expression VisitBackendProbeVariableExpression(VclParser.BackendProbeVariableExpressionContext context)
        {
            base.VisitBackendProbeVariableExpression(context);

            var normalisedMemberName = context.name.Text.Replace("_", "");
            var propInfo = typeof(VclProbe).GetProperty(
                normalisedMemberName,
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.IgnoreCase |
                BindingFlags.SetProperty);
            CurrentBackendBindings.Add(
                Expression.Bind(
                    propInfo,
                    VisitProbeExpression(context.probeExpression())));

            return null;
        }

        public override Expression VisitProbeDeclaration(VclParser.ProbeDeclarationContext context)
        {
            // Named probe declarations are already handled elsewhere
            return null;
        }

        public override Expression VisitProbeExpression(VclParser.ProbeExpressionContext context)
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

        public override Expression VisitProbeReferenceExpression(VclParser.ProbeReferenceExpressionContext context)
        {
            var probeName = context.probeName.Text;
            if (!ProbeExpressions.ContainsKey(probeName))
            {
                throw new ArgumentException($"Named probe ({probeName}) not found");
            }

            return ProbeExpressions[probeName];
        }

        public override Expression VisitProbeInlineExpression(VclParser.ProbeInlineExpressionContext context)
        {
            CurrentProbeBindings.Clear();
            base.VisitProbeInlineExpression(context);

            var probeTypeCtor = typeof(VclProbe).GetConstructor(new[] { typeof(string) });
            return Expression.MemberInit(
                Expression.New(probeTypeCtor, Expression.Constant($"Anonymous<{CurrentBackendName}>")),
                CurrentProbeBindings);
        }
    }
}