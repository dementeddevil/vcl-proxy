using System;
using System.CodeDom;
using Antlr4.Runtime.Misc;

namespace Im.Proxy.VclCore.Compiler
{
    public class VclBaseExpressionVisitor : VclLangBaseVisitor<CodeObject>
    {
        protected VclBaseExpressionVisitor(VclCompilerContext compilerContext)
        {
            CompilerContext = compilerContext;
        }

        protected VclCompilerContext CompilerContext { get; }

        public override CodeObject VisitStringLiteral(VclLangParser.StringLiteralContext context)
        {
            base.VisitStringLiteral(context);
            return new CodePrimitiveExpression(context.value.Text.Trim('"'));
        }

        public override CodeObject VisitSynthenticLiteral([NotNull] VclLangParser.SynthenticLiteralContext context)
        {
            base.VisitSynthenticLiteral(context);
            return new CodePrimitiveExpression(context.value.Text.Trim('"', '{', '}'));
        }

        public override CodeObject VisitIntegerLiteral(VclLangParser.IntegerLiteralContext context)
        {
            base.VisitIntegerLiteral(context);
            return new CodePrimitiveExpression(int.Parse(context.value.Text));
        }

        public override CodeObject VisitTimeLiteral(VclLangParser.TimeLiteralContext context)
        {
            base.VisitTimeLiteral(context);
            var rawValue = context.value.Text;
            TimeSpan value;
            if (rawValue.EndsWith("ms"))
            {
                var timeComponentText = rawValue.Substring(0, rawValue.Length - 2);
                value = TimeSpan.FromMilliseconds(int.Parse(timeComponentText));
            }
            else
            {
                var timeComponent = int.Parse(rawValue.Substring(0, rawValue.Length - 1));
                switch (rawValue.Substring(rawValue.Length - 1, 1).ToLower())
                {
                    case "s":
                        value = TimeSpan.FromSeconds(timeComponent);
                        break;
                    case "m":
                        value = TimeSpan.FromMinutes(timeComponent);
                        break;
                    case "d":
                        value = TimeSpan.FromDays(timeComponent);
                        break;
                    case "w":
                        value = TimeSpan.FromDays(7 * timeComponent);
                        break;
                    case "y":
                        var refTimeStamp = DateTime.UtcNow;
                        value = refTimeStamp.AddYears(timeComponent) - refTimeStamp;
                        break;
                    default:
                        throw new InvalidOperationException("Unable to parse time component");
                }
            }

            return new CodePrimitiveExpression(value);
        }

        public override CodeObject VisitBooleanLiteral(VclLangParser.BooleanLiteralContext context)
        {
            base.VisitBooleanLiteral(context);
            return new CodePrimitiveExpression(bool.Parse(context.value.Text));
        }
    }
}