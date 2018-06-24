using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using Im.Proxy.VclCore.Model;

namespace Im.Proxy.VclCore.Compiler
{
    public class VclCompileNamedAclObjects : VclBaseExpressionVisitor
    {
        private Expression _currentAclBuilder;

        public IDictionary<string, Expression> AclExpressions =
            new Dictionary<string, Expression>(StringComparer.OrdinalIgnoreCase);

        public override Expression VisitAclDeclaration(VclParser.AclDeclarationContext context)
        {
            _currentAclBuilder = null;

            base.VisitAclDeclaration(context);

            var name = context.name.Text;
            if (_currentAclBuilder != null)
            {
                _currentAclBuilder = Expression.Call(
                    _currentAclBuilder,
                    typeof(VclAclBuilder).GetMethod(
                        nameof(VclAclBuilder.SetName),
                        new[] { typeof(string) }),
                    Expression.Constant(name));

                AclExpressions.Add(name, Expression.Call(
                    _currentAclBuilder,
                    typeof(VclAclBuilder).GetMethod(
                        nameof(VclAclBuilder.Build),
                        new Type[0])));
            }

            return null;
        }

        public override Expression VisitAclElement(VclParser.AclElementContext context)
        {
            var expression = base.VisitAclElement(context);

            if (_currentAclBuilder == null)
            {
                _currentAclBuilder = Expression.New(
                    typeof(VclAclBuilder).GetConstructor(new Type[0]));
            }

            if (context.exclude != null)
            {
                _currentAclBuilder = Expression.Call(
                    _currentAclBuilder,
                    typeof(VclAclBuilder).GetMethod(
                        nameof(VclAclBuilder.Exclude),
                        new[] {typeof(VclAclEntry)}),
                    expression);
            }
            else
            {
                _currentAclBuilder = Expression.Call(
                    _currentAclBuilder,
                    typeof(VclAclBuilder).GetMethod(
                        nameof(VclAclBuilder.Include),
                        new[] { typeof(VclAclEntry) }),
                    expression);
            }

            return null;
        }

        public override Expression VisitAclEntryNonIgnorableHost(VclParser.AclEntryNonIgnorableHostContext context)
        {
            base.VisitAclEntryNonIgnorableHost(context);

            var mi = typeof(VclAclEntry).GetMethod(
                nameof(VclAclEntry.FromHostName),
                new[] { typeof(string), typeof(bool) });
            return Expression.Call(
                mi,
                Expression.Constant(context.host.Text),
                Expression.Constant(false));
        }

        public override Expression VisitAclEntryIgnorableHost(VclParser.AclEntryIgnorableHostContext context)
        {
            base.VisitAclEntryIgnorableHost(context);

            var mi = typeof(VclAclEntry).GetMethod(
                nameof(VclAclEntry.FromHostName),
                new[] { typeof(string), typeof(bool) });
            return Expression.Call(
                mi,
                Expression.Constant(context.host.Text),
                Expression.Constant(true));
        }

        public override Expression VisitAclEntryIpAddress(VclParser.AclEntryIpAddressContext context)
        {
            base.VisitAclEntryIpAddress(context);

            var mi = typeof(VclAclEntry).GetMethod(
                nameof(VclAclEntry.FromAddress),
                new[] { typeof(string) });
            return Expression.Call(
                mi,
                Expression.Constant(context.address.Text));
        }

        public override Expression VisitAclEntrySubnetMask(VclParser.AclEntrySubnetMaskContext context)
        {
            base.VisitAclEntrySubnetMask(context);

            var mi = typeof(VclAclEntry).GetMethod(
                nameof(VclAclEntry.FromSubnet),
                new[] { typeof(string) });
            return Expression.Call(
                mi,
                Expression.Constant(context.subnet.Text));
        }

        protected override Expression AggregateResult(Expression aggregate, Expression nextResult)
        {
            return nextResult ?? aggregate;
        }
    }
}