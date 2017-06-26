using Antlr4.Runtime;
using Antlr4.Runtime.Misc;
using Antlr4.Runtime.Tree;

namespace DuckBot.DuckScript
{
    class SwitchEvaluator : DuckSwitchBaseVisitor<string>
    {
        private ScriptEvaluator ScriptEvaluator { get; }

        public SwitchEvaluator(CmdContext ctx) => ScriptEvaluator = new ScriptEvaluator(ctx);

        public string Evaluate(string script)
        {
            ICharStream input = new AntlrInputStream(script);
            ITokenSource lexer = new DuckSwitchLexer(input);
            ITokenStream stream = new CommonTokenStream(lexer);
            DuckSwitchParser parser = new DuckSwitchParser(stream) { ErrorHandler = new BailErrorStrategy() };
            return Visit(parser.content());
        }

        protected override string AggregateResult(string aggregate, string nextResult) => aggregate + nextResult;

        protected override string DefaultResult => "";

        public override string VisitContent([NotNull] DuckSwitchParser.ContentContext context)
        {
            string switchVal = context.VALUE().GetText();
            foreach (DuckSwitchParser.OptionCaseContext optCase in context.optionCase())
            {
                ITerminalNode[] values = optCase.VALUE();
                if (switchVal.Equals(ValueOrEmpty(values[0])))
                {
                    return values.Length > 1 ? ValueOrEmpty(values[1]) : "";
                }
            }
            DuckSwitchParser.OptionDefaultContext optDef = context.optionDefault();
            return optDef == null ? null : ValueOrEmpty(optDef.VALUE());
        }

        private string ValueOrEmpty(ITerminalNode value) => value == null ? "" : value.GetText();
    }
}
