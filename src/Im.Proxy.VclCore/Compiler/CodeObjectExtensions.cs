using System;
using System.CodeDom;

namespace Im.Proxy.VclCore.Compiler
{
    public static class CodeObjectExtensions
    {
        private static readonly string TypeKey = "type";

        public static Type GetExpressionType(this CodeObject instance)
        {
            return (Type)instance.UserData[TypeKey];
        }

        public static T SetExpressionType<T>(this T instance, Type type)
            where T : CodeObject
        {
            instance.UserData[TypeKey] = type;
            return instance;
        }
    }
}