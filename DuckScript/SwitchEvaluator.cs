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

        public override string VisitTerminal(ITerminalNode node) => node.GetText();

        public override string VisitValue([NotNull] DuckSwitchParser.ValueContext context)
        {
            string text = context.GetText();
            if (text.EndsWith("\r\n")) text = text.Remove(text.Length - 2);
            else if (text.EndsWith("\n")) text = text.Remove(text.Length - 1);
            return ScriptEvaluator.Evaluate(text);
        }

        public override string VisitContent([NotNull] DuckSwitchParser.ContentContext context)
        {
            string switchVal = context.value().Accept(this);
            foreach (DuckSwitchParser.OptionCaseContext optCase in context.optionCase())
            {
                DuckSwitchParser.ValueContext[] values = optCase.value();
                if (switchVal.Equals(ValueOrEmpty(values[0])))
                {
                    return ValueOrEmpty(values.Length > 1 ? values[1] : null);
                }
            }
            return context.optionDefault()?.value().Accept(this);
        }

        private string ValueOrEmpty(DuckSwitchParser.ValueContext value) => value == null ? "" : value.Accept(this);
    }
}
