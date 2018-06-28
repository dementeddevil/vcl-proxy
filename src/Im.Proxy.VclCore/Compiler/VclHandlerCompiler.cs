using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using Im.Proxy.VclCore.Model;

namespace Im.Proxy.VclCore.Compiler
{
    public class VclHandlerCompiler : VclBaseVisitor<Expression>
    {
        private IList<Expression> _vclInitExpressions = new List<Expression>();

        public VclHandlerCompiler(VclHandlerCompilerContext context)
        {
            var assemblyName = new AssemblyName("NameOfVclFile");
            var assemblyBuilder = AssemblyBuilder
                .DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.RunAndSave);
            var assemblyModule = assemblyBuilder.DefineDynamicModule("MyDynamicModule");
            var handlerTypeBuilder = assemblyModule
                .DefineType(
                    "VclHandlerImpl",
                    TypeAttributes.Public | TypeAttributes.Class,
                    typeof(VclHandler));

            // Hoist into wrapper class for tracking subroutine content
            var vclInitMethodBuilder = handlerTypeBuilder
                .DefineMethod(
                    "VclInit",
                    MethodAttributes.Family,
                    typeof(void),
                    new[]
                    {
                        typeof(VclContext)
                    });
            handlerTypeBuilder.DefineMethodOverride(
                vclInitMethodBuilder,
                typeof(VclHandler).GetMethod("VclInit", BindingFlags.Instance | BindingFlags.NonPublic));

            var vclInitCompoundStatement = Expression.Block(_vclInitExpressions);
            //Expression<Action> vclInitExpr= Expression.Lambda<Action>(vclInitCompoundStatement,); 
            //vclInitExpr.C vclInitCompoundStatement.
        }

        public override Expression VisitBackendDeclaration(VclParser.BackendDeclarationContext context)
        {
            return base.VisitBackendDeclaration(context);
        }
    }
}