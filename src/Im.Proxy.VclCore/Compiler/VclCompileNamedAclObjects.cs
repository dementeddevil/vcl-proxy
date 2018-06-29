using System;
using System.CodeDom;
using Im.Proxy.VclCore.Model;

namespace Im.Proxy.VclCore.Compiler
{
    public class VclCompileNamedAclObjects : VclBaseExpressionVisitor
    {
        public VclCompileNamedAclObjects(VclCompilerContext compilerContext)
            : base(compilerContext)
        {
        }

        private CodeExpression _currentAclBuilder;

        public override CodeObject VisitAclDeclaration(VclParser.AclDeclarationContext context)
        {
            _currentAclBuilder = null;

            var name = context.name.Text;
            if (CompilerContext.AclReferences.ContainsKey(name))
            {
                throw new ArgumentException("Acl name is not unique");
            }

            base.VisitAclDeclaration(context);

            if (_currentAclBuilder != null)
            {
                // Determine field name
                var fieldName = name.SafeIdentifier("_acl");

                // Setup current probe object and add to probe mapper
                CompilerContext.AclReferences.Add(
                    name,
                    new CodeFieldReferenceExpression(
                        new CodeThisReferenceExpression(), fieldName));

                _currentAclBuilder =
                    new CodeMethodInvokeExpression(
                        new CodeMethodReferenceExpression(
                            _currentAclBuilder,
                            nameof(VclAclBuilder.SetName),
                            new CodeTypeReference(typeof(string))),
                        new CodePrimitiveExpression(name));

                CompilerContext.HandlerClass.Members.Add(
                    new CodeMemberField(typeof(VclProbe), fieldName)
                    {
                        Attributes = MemberAttributes.Private,
                        InitExpression =
                            new CodeMethodInvokeExpression(
                                new CodeMethodReferenceExpression(
                                    _currentAclBuilder,
                                    nameof(VclAclBuilder.Build)))
                    });
            }

            return null;
        }

        public override CodeObject VisitAclElement(VclParser.AclElementContext context)
        {
            var expression = base.VisitAclElement(context);

            if (_currentAclBuilder == null)
            {
                _currentAclBuilder = new CodeObjectCreateExpression(typeof(VclAclBuilder));
            }

            if (context.exclude != null)
            {
                _currentAclBuilder = 
                    new CodeMethodInvokeExpression(
                        new CodeMethodReferenceExpression(
                            _currentAclBuilder,
                            nameof(VclAclBuilder.Exclude),
                            new CodeTypeReference(typeof(VclAclEntry))),
                        (CodeExpression)expression);
            }
            else
            {
                _currentAclBuilder =
                    new CodeMethodInvokeExpression(
                        new CodeMethodReferenceExpression(
                            _currentAclBuilder,
                            nameof(VclAclBuilder.Include),
                            new CodeTypeReference(typeof(VclAclEntry))),
                        (CodeExpression)expression);
            }

            return null;
        }

        public override CodeObject VisitAclEntryNonIgnorableHost(VclParser.AclEntryNonIgnorableHostContext context)
        {
            base.VisitAclEntryNonIgnorableHost(context);

            return new CodeMethodInvokeExpression(
                new CodeMethodReferenceExpression(
                    new CodeTypeReferenceExpression(typeof(VclAclEntry)),
                    nameof(VclAclEntry.FromHostName),
                    new CodeTypeReference(typeof(string)),
                    new CodeTypeReference(typeof(bool))),
                new CodePrimitiveExpression(context.host.Text),
                new CodePrimitiveExpression(false));
        }

        public override CodeObject VisitAclEntryIgnorableHost(VclParser.AclEntryIgnorableHostContext context)
        {
            base.VisitAclEntryIgnorableHost(context);

            return new CodeMethodInvokeExpression(
                new CodeMethodReferenceExpression(
                    new CodeTypeReferenceExpression(typeof(VclAclEntry)),
                    nameof(VclAclEntry.FromHostName),
                    new CodeTypeReference(typeof(string)),
                    new CodeTypeReference(typeof(bool))),
                new CodePrimitiveExpression(context.host.Text),
                new CodePrimitiveExpression(true));
        }

        public override CodeObject VisitAclEntryIpAddress(VclParser.AclEntryIpAddressContext context)
        {
            base.VisitAclEntryIpAddress(context);

            return new CodeMethodInvokeExpression(
                new CodeMethodReferenceExpression(
                    new CodeTypeReferenceExpression(typeof(VclAclEntry)),
                    nameof(VclAclEntry.FromAddress),
                    new CodeTypeReference(typeof(string))),
                new CodePrimitiveExpression(context.address.Text));
        }

        public override CodeObject VisitAclEntrySubnetMask(VclParser.AclEntrySubnetMaskContext context)
        {
            base.VisitAclEntrySubnetMask(context);

            return new CodeMethodInvokeExpression(
                new CodeMethodReferenceExpression(
                    new CodeTypeReferenceExpression(typeof(VclAclEntry)),
                    nameof(VclAclEntry.FromSubnet),
                    new CodeTypeReference(typeof(string))),
                new CodePrimitiveExpression(context.subnet.Text));
        }

        protected override CodeObject AggregateResult(CodeObject aggregate, CodeObject nextResult)
        {
            return nextResult ?? aggregate;
        }
    }
}