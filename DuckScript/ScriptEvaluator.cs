using System.Text;
using Antlr4.Runtime;
using Antlr4.Runtime.Misc;

namespace DuckBot.DuckScript
{
    class ScriptEvaluator : DuckScriptBaseVisitor<StringBuilder>
    {
        private CmdContext Context { get; }

        public ScriptEvaluator(CmdContext ctx)
        {
            Context = ctx;
        }

        public string Evaluate(string script)
        {
            AntlrInputStream input = new AntlrInputStream(script);
            DuckScriptLexer lexer = new DuckScriptLexer(input);
            CommonTokenStream stream = new CommonTokenStream(lexer);
            DuckScriptParser parser = new DuckScriptParser(stream);
            return parser.content().Accept(this).ToString();
        }

        protected override StringBuilder AggregateResult(StringBuilder aggregate, StringBuilder nextResult) => aggregate.Append(nextResult);

        protected override StringBuilder DefaultResult => new StringBuilder();

        public override StringBuilder VisitFunction([NotNull] DuckScriptParser.FunctionContext context)
        {
            DuckScriptParser.ContentContext[] data = context.content();
            string[] args = new string[data.Length];
            for (int i = data.Length - 1; i >= 0; --i)
                args[i] = data[i].Accept(this).ToString();
            return new StringBuilder(FuncVar.Run(context.FUNC_NAME().GetText(), args, Context));
        }
    }
}
