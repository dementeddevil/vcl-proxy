using System;
using System.Linq.Expressions;

namespace Im.Proxy.VclCore.Compiler
{
    public class VclBaseExpressionVisitor : VclBaseVisitor<Expression>
    {
        public override Expression VisitStringLiteral(VclParser.StringLiteralContext context)
        {
            return Expression.Constant(context.StringConstant().GetText());
        }

        public override Expression VisitIntegerLiteral(VclParser.IntegerLiteralContext context)
        {
            return Expression.Constant(int.Parse(context.IntegerConstant().GetText()));
        }

        public override Expression VisitTimeLiteral(VclParser.TimeLiteralContext context)
        {
            var rawValue = context.TimeConstant().GetText();
            var value = TimeSpan.Zero;
            if (rawValue.EndsWith("ms"))
            {
                var timeComponentText = rawValue.Substring(0, rawValue.Length - 2);
                value = TimeSpan.FromMilliseconds(int.Parse(timeComponentText));
            }
            else
            {
                var timeComponentText = rawValue.Substring(0, rawValue.Length - 1);
                switch (rawValue.Substring(rawValue.Length - 1, 1).ToLower())
                {
                    case "s":
                        value = TimeSpan.FromMilliseconds(int.Parse(timeComponentText));
                        break;
                    case "m":
                        value = TimeSpan.FromMilliseconds(int.Parse(timeComponentText));
                        break;
                    case "d":
                        value = TimeSpan.FromMilliseconds(int.Parse(timeComponentText));
                        break;
                    case "w":
                        value = TimeSpan.FromMilliseconds(int.Parse(timeComponentText));
                        break;
                    case "y":
                        value = TimeSpan.FromMilliseconds(int.Parse(timeComponentText));
                        break;
                    default:
                        throw new InvalidOperationException("Unable to parse time component");
                }
            }

            return Expression.Constant(value);
        }

    }
}