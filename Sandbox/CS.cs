using System;
using System.IO;
using System.Text;
using System.Security;
using System.Reflection;
using System.Globalization;
using System.Threading.Tasks;
using System.CodeDom.Compiler;
using System.Security.Permissions;
using DuckBot.Resources;

namespace DuckBot.Sandbox
{

    public sealed class CS : MarshalByRefObject
    {
        public CS() { }

        private static string Compile(string content)
        {
            const string template = "using System;using System.Net;using System.Collections.Generic;using Discord;namespace DuckBot {public static class Script {public static string Code(string rawText,dynamic sender,dynamic server,dynamic channel){\n";
            string source = template + content + "}}}";
            using (CodeDomProvider compiler = CodeDomProvider.CreateProvider("CSharp"))
            {
                Assembly ass = typeof(Program).Assembly;
                CompilerParameters pars = new CompilerParameters();
                foreach (AssemblyName an in ass.GetReferencedAssemblies())
                    pars.ReferencedAssemblies.Add(Assembly.ReflectionOnlyLoad(an.FullName).Location);
                pars.ReferencedAssemblies.Add(ass.Location);
                pars.GenerateExecutable = false;
                pars.GenerateInMemory = false;
                pars.OutputAssembly = Guid.NewGuid() + ".dll";
                CompilerResults results = compiler.CompileAssemblyFromSource(pars, source);
                if (!results.Errors.HasErrors) return Path.GetFullPath(pars.OutputAssembly);
                else
                {
                    StringBuilder errors = new StringBuilder(Strings.err_compile + ": ");
                    errors.AppendFormat("{0},{1}: ``` {2} ```", results.Errors[0].Line - 1, results.Errors[0].Column, results.Errors[0].ErrorText);
                    throw new FormatException(errors.ToString());
                }
            }
        }

        /// <summary>Added just to reference dynamic types, useless</summary>
        public static dynamic DynRef(dynamic arg) { return arg.ToString(); }

        internal static string Execute(string content, CmdContext msg)
        {
            AppDomainSetup setup = new AppDomainSetup();
            setup.ApplicationBase = AppDomain.CurrentDomain.BaseDirectory;
            setup.DisallowBindingRedirects = true;
            setup.DisallowCodeDownload = true;
            setup.DisallowPublisherPolicy = true;
            PermissionSet ps = new PermissionSet(PermissionState.None);
            ps.AddPermission(new FileIOPermission(FileIOPermissionAccess.Read | FileIOPermissionAccess.PathDiscovery, setup.ApplicationBase));
            ps.AddPermission(new SecurityPermission(SecurityPermissionFlag.Execution | SecurityPermissionFlag.UnmanagedCode | SecurityPermissionFlag.ControlThread));
            ps.AddPermission(new System.Net.WebPermission(PermissionState.Unrestricted));
            AppDomain app = null;
            string dll = null;
            try
            {
                dll = Compile(content);
                app = AppDomain.CreateDomain(dll, null, setup, ps);
                CS obj = (CS)app.CreateInstanceAndUnwrap(typeof(CS).Assembly.FullName, typeof(CS).FullName);
                using (StringWriter sw = new StringWriter())
                {
                    sw.WriteLine(obj.Remote(CultureInfo.CurrentCulture, sw, dll, msg.Args, Proxy.GetProxy(msg.Sender), Proxy.GetProxy(msg.Server), Proxy.GetProxy(msg.Channel)));
                    string res = sw.ToString().Trim();
                    return res.Length == 0 ? Strings.ret_empty_script : res;
                }
            }
            catch (FormatException ex) { return ex.Message; }
            finally
            {
                if (app != null) AppDomain.Unload(app);
                if (dll != null) File.Delete(dll);
            }
        }

        private string Remote(CultureInfo ci, StringWriter sw, string assembly, string args, Discord.IGuildUser sender, Discord.IGuild server, Discord.ITextChannel channel)
        {
            CultureInfo.DefaultThreadCurrentCulture = ci;
            CultureInfo.DefaultThreadCurrentUICulture = ci;
            Console.SetOut(sw);
            Console.SetError(sw);
            Assembly script = Assembly.LoadFrom(assembly);
            MethodInfo method = script.GetType("DuckBot.Script").GetMethod("Code");
            Task<string> task = Task.Run(() => (string)method.Invoke(null, new object[] { args, sender, server, channel }));
            return task.Wait(90000) ? task.Result : Strings.err_scrtimeout;
        }
    }
}
