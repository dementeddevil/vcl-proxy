using System;
using System.Linq.Expressions;
using Antlr4.Runtime.Misc;

namespace Im.Proxy.VclCore.Compiler
{
    public class VclBaseExpressionVisitor : VclBaseVisitor<Expression>
    {
        public override Expression VisitStringLiteral(VclParser.StringLiteralContext context)
        {
            base.VisitStringLiteral(context);
            return Expression.Constant(context.StringConstant().GetText().Trim('"'));
        }

        public override Expression VisitSynthenticLiteral([NotNull] VclParser.SynthenticLiteralContext context)
        {
            base.VisitSynthenticLiteral(context);
            return Expression.Constant(context.SyntheticString().GetText().Trim('"', '{', '}'));
        }

        public override Expression VisitIntegerLiteral(VclParser.IntegerLiteralContext context)
        {
            base.VisitIntegerLiteral(context);
            return Expression.Constant(int.Parse(context.IntegerConstant().GetText()));
        }

        public override Expression VisitTimeLiteral(VclParser.TimeLiteralContext context)
        {
            base.VisitTimeLiteral(context);
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
                        value = TimeSpan.FromSeconds(int.Parse(timeComponentText));
                        break;
                    case "m":
                        value = TimeSpan.FromMinutes(int.Parse(timeComponentText));
                        break;
                    case "d":
                        value = TimeSpan.FromDays(int.Parse(timeComponentText));
                        break;
                    case "w":
                        value = TimeSpan.FromDays(7 * int.Parse(timeComponentText));
                        break;
                    case "y":
                        value = TimeSpan.FromDays(365 * int.Parse(timeComponentText));
                        break;
                    default:
                        throw new InvalidOperationException("Unable to parse time component");
                }
            }

            return Expression.Constant(value);
        }

        public override Expression VisitBooleanLiteral(VclParser.BooleanLiteralContext context)
        {
            base.VisitBooleanLiteral(context);
            return Expression.Constant(Boolean.Parse(context.GetText()));
        }
    }
}