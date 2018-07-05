using System;
using System.CodeDom;
using System.Collections.Generic;

namespace Im.Proxy.VclCore.Compiler
{
    public class VclCompilerContext
    {
        public VclCompilerContext()
        {
            HandlerClass = new CodeTypeDeclaration("VclCustomHandler");
            HandlerClass.BaseTypes.Add(typeof(VclHandler));
            HandlerClass.IsClass = true;
            HandlerClass.Comments.Add(
                new CodeCommentStatement(
                    @"<summary>
<c>VclCustomHandler</c> extends <see cref=""VclHandler"" /> by integrating
custom actions derived from parsing a custom VCL file.
</summary>
<remarks>
For further information about Varnish Configuration Language please head over
to: https://book.varnish-software.com/4.0/
</remarks>",
                    true));
        }

        public CodeTypeDeclaration HandlerClass { get; }

        public IDictionary<string, CodeFieldReferenceExpression> ProbeReferences { get; } =
            new Dictionary<string, CodeFieldReferenceExpression>(StringComparer.OrdinalIgnoreCase);

        public IDictionary<string, CodeFieldReferenceExpression> AclReferences { get; } =
            new Dictionary<string, CodeFieldReferenceExpression>(StringComparer.OrdinalIgnoreCase);

        public IDictionary<string, CodeFieldReferenceExpression> BackendReferences { get; } =
            new Dictionary<string, CodeFieldReferenceExpression>(StringComparer.OrdinalIgnoreCase);

        public IDictionary<string, CodeMemberMethod> MethodDefinitions { get; } =
            new Dictionary<string, CodeMemberMethod>(StringComparer.OrdinalIgnoreCase);

        public CodeStatementCollection GetOrCreateSystemMethodStatements(string methodName)
        {
            if (!MethodDefinitions.TryGetValue(methodName, out var codeMemberMethod))
            {
                codeMemberMethod = CreateSystemMethod(methodName);
                HandlerClass.Members.Add(codeMemberMethod);
                MethodDefinitions.Add(methodName, codeMemberMethod);
            }

            return codeMemberMethod.Statements;
        }

        public CodeStatementCollection CreateCustomMethodStatements(string methodName)
        {
            if (MethodDefinitions.TryGetValue(methodName, out var codeMemberMethod))
            {
                throw new ArgumentException($"Method {methodName} is already defined.");
            }

            codeMemberMethod = CreateCustomMethod(methodName);
            HandlerClass.Members.Add(codeMemberMethod);
            MethodDefinitions.Add(methodName, codeMemberMethod);

            return codeMemberMethod.Statements;
        }

        public static CodeMemberMethod CreateSystemMethod(string methodName)
        {
            var systemMethodName = SystemFunctionToMethodInfoFactory
                .GetSystemMethodName(methodName);

            // Create code method
            return
                new CodeMemberMethod
                {
                    Name = systemMethodName,
                    // ReSharper disable once BitwiseOperatorOnEnumWithoutFlags
                    Attributes = MemberAttributes.Public | MemberAttributes.Override,
                    ReturnType = new CodeTypeReference(typeof(VclAction))
                };
        }

        public static CodeMemberMethod CreateCustomMethod(string methodName)
        {
            // Create code method
            return
                new CodeMemberMethod
                {
                    Name = methodName.SafeIdentifier(),
                    Attributes = MemberAttributes.Public,
                    ReturnType = new CodeTypeReference(typeof(VclAction))
                };
        }
    }
}