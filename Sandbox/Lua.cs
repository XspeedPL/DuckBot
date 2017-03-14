using System;
using System.IO;
using NLua;
using DuckBot.Resources;

namespace DuckBot.Sandbox
{
    internal static class Lua
    {
        internal static string Execute(string content, CmdParams msg)
        {
            using (NLua.Lua lua = new NLua.Lua())
            using (PrintProxy proxy = new PrintProxy())
            {
                lua.RegisterFunction("print", proxy, proxy.GetType().GetMethod("Print", new Type[] { typeof(object[]) }));
                string code;
                using (StreamReader sr = new StreamReader(typeof(Lua).Assembly.GetManifestResourceStream("DuckBot.Resources.Sandbox.lua")))
                    code = sr.ReadToEnd();
                try
                {
                    const string template = "args = {...};rawText,sender,server,channel=args[1],args[2],args[3],args[4]\n";
                    string source = template + content;
                    using (LuaFunction func = (LuaFunction)lua.DoString(code, "sandbox")[0])
                    {
                        object[] rets = func.Call(source, msg.Args, msg.Sender, msg.Server, msg.Channel);
                        if (rets.Length >= 2)
                        {
                            object[] arr = new object[rets.Length - 1];
                            Array.Copy(rets, 1, arr, 0, arr.Length);
                            proxy.Print(arr);
                        }
                        string res = proxy.ToString().Trim();
                        return res.Length == 0 ? Strings.ret_empty_script : res;
                    }
                }
                catch (NLua.Exceptions.LuaScriptException ex) { return Strings.err_generic + ": " + ex.Message + "\n``` " + ex.Source + " ```"; }
            }
        }

        private sealed class PrintProxy : StringWriter
        {
            public void Print(params object[] args)
            {
                if (args.Length > 0)
                {
                    Write(args[0]);
                    for (int i = 1; i < args.Length; ++i)
                    {
                        Write("    ");
                        Write(args[i]);
                    }
                }
                WriteLine();
            }
        }
    }
}
