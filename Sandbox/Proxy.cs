using System;
using Castle.DynamicProxy;

namespace DuckBot.Sandbox
{
    public static class Proxy
    {
        public static T GetProxy<T>(T instance)
        {
            Type retType = typeof(T);
            if (!retType.IsInterface) throw new ArgumentException("Type must be an interface");
            ProxyGenerator generator = new ProxyGenerator();
            ProxyGenerationOptions options = new ProxyGenerationOptions() { BaseTypeForInterfaceProxy = typeof(MarshalByRefObject) };
            return (T)generator.CreateInterfaceProxyWithTarget(retType, instance, options);
        }
    }
}
