using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using System.CodeDom.Compiler;
using System.CodeDom;
using System.Reflection;
using Microsoft.CSharp;

namespace DuckBot
{
    public class Command
    {
        public string Content { get; private set; }
        public string Type { get; private set; }
        public DateTime CreationDate { get; private set; }
        public string Creator { get; private set; }
        public MethodInfo csharpCommand { get; internal set; }

        public Command(string type, string content, string creator)
        {
            Type = type;
            Content = content;
            CreationDate = DateTime.Now;
            Creator = creator;
        }
        public string Run(string user, string userAsMention, string input, MessageEventArgs eventArgs)
        {
            try
            {

                if (Type == "lua")
                {
                    /*Lua lua = new Lua();
                     string toDo = Content;
                     toDo = toDo.Replace("%user%", user);
                     toDo = toDo.Replace("%userAsMention%", userAsMention);
                     toDo = toDo.Replace("%input%", input);
                     try
                     {
                         string toRun = $"local user = \"{user}\"{Environment.NewLine}local userAsMention = \"{userAsMention}\"{Environment.NewLine}local input = \"{input}\" {Environment.NewLine} {toDo}";
                         object[] ran = lua.DoString(toRun);
                         string toReturn = ran[0].ToString();
                         /*for (int i = 0; i < ran.Length; i++)
                         {
                             toReturn += $"{ran[i]} \n";
                         }

                         return toReturn;
                     }
                     catch (Exception e)
                     {
                         return e.ToString();
                     }*/
                    return "Lua is temporarily (OR FOREVER) dead. Learn C#.";
                }
                else if (Type == "csharpscript" || Type == "csharp")
                {
                    string sourceTemplate =
                    @"using System; 
using System.Collections.Generic;
using Discord.Net; 
using Discord;
using Discord.Commands;
using Newtonsoft.Json;
namespace DuckCommand { 
public class Command {
public static string Main(string input, MessageEventArgs e)
{
@Placeholder
} 
}
}";

                    string snippet = Content;

                    string sourceCode = sourceTemplate.Replace("@Placeholder", snippet);
                    CodeSnippetCompileUnit snippetCompileUnit = new CodeSnippetCompileUnit(sourceCode);

                    using (CSharpCodeProvider provider = new CSharpCodeProvider(new Dictionary<String, String> { { "CompilerVersion", "v4.0" } }))
                    {
                        CompilerParameters parameters = new CompilerParameters();
                        parameters.ReferencedAssemblies.Add("System.dll");
                        parameters.ReferencedAssemblies.Add("Discord.Net.dll");
                        parameters.ReferencedAssemblies.Add("Discord.Net.Commands.dll");
                        parameters.ReferencedAssemblies.Add("Newtonsoft.Json.dll");
                        parameters.GenerateExecutable = false;
                        parameters.GenerateInMemory = true;
                        parameters.IncludeDebugInformation = false;

                        CompilerResults results = provider.CompileAssemblyFromDom(parameters, snippetCompileUnit);

                        if (!results.Errors.HasErrors)
                        {
                            Type type = results.CompiledAssembly.GetType("DuckCommand.Command");
                            MethodInfo method = type.GetMethod("Main");
                            return method.Invoke(null, new object[] { input, eventArgs }).ToString();
                        }
                        else
                        {
                            StringBuilder sb = new StringBuilder();
                            foreach (CompilerError compilerError in results.Errors)
                                sb.AppendFormat("Error in line {0}:\n\n{1}", compilerError.Line, compilerError.ErrorText);
                            return sb.ToString();
                        }
                    }
                }
                else
                {
                    string toDo = Content;
                    toDo = toDo.Replace("%user%", user);
                    toDo = toDo.Replace("%userAsMention%", userAsMention);
                    toDo = toDo.Replace("%input%", input);
                    toDo = toDo.Replace("%userOrInput%", input != "" ? input : user);
                    return toDo;
                }
            }
            catch (Exception e)
            {
                Program.Log(e.ToString(), true);
                return "Welp, an exception occured. Ping EchoDuck to see it if you need to.";
            }
        }
    }

}

