using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
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
        private IList<MemberBinding> CurrentProbeBindings { get; } = new List<MemberBinding>();

        public IDictionary<string, Expression> ProbeExpressions { get; } =
            new Dictionary<string, Expression>(StringComparer.OrdinalIgnoreCase);

        public override Expression VisitProbeDeclaration(VclParser.ProbeDeclarationContext context)
        {
            var name = context.Identifier().GetText();
            if (ProbeExpressions.ContainsKey(name))
            {
                throw new ArgumentException("Probe name is not unique");
            }

            CurrentProbeBindings.Clear();
            base.VisitProbeDeclaration(context);

            var probeTypeCtor = typeof(VclProbe).GetConstructor(new[] { typeof(string) });
            ProbeExpressions.Add(
                name,
                Expression.MemberInit(
                    Expression.New(probeTypeCtor, Expression.Constant(name)),
                    CurrentProbeBindings));

            return null;
        }

        public override Expression VisitProbeStringVariableExpression(VclParser.ProbeStringVariableExpressionContext context)
        {
            base.VisitProbeStringVariableExpression(context);

            var normalisedMemberName = context.name.GetText().Replace("_", "");

            var propInfo = typeof(VclProbe).GetProperty(
                normalisedMemberName,
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.IgnoreCase |
                BindingFlags.SetProperty);
            CurrentProbeBindings.Add(
                Expression.Bind(
                    propInfo,
                    VisitStringLiteral(context.stringLiteral())));

            return null;
        }

        public override Expression VisitProbeIntegerVariableExpression(VclParser.ProbeIntegerVariableExpressionContext context)
        {
            base.VisitProbeIntegerVariableExpression(context);

            var normalisedMemberName = context.name.GetText().Replace("_", "");
            var propInfo = typeof(VclProbe).GetProperty(
                normalisedMemberName,
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.IgnoreCase |
                BindingFlags.SetProperty);
            CurrentProbeBindings.Add(
                Expression.Bind(
                    propInfo,
                    VisitIntegerLiteral(context.integerLiteral())));

            return null;
        }

        public override Expression VisitProbeTimeVariableExpression(VclParser.ProbeTimeVariableExpressionContext context)
        {
            base.VisitProbeTimeVariableExpression(context);

            var normalisedMemberName = context.name.GetText().Replace("_", "");
            var propInfo = typeof(VclProbe).GetProperty(
                normalisedMemberName,
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.IgnoreCase |
                BindingFlags.SetProperty);
            CurrentProbeBindings.Add(
                Expression.Bind(
                    propInfo,
                    VisitTimeLiteral(context.timeLiteral())));

            return null;
        }
    }
}