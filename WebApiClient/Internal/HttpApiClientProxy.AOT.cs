﻿#if AOT
using System;
using System.Linq;
using System.Reflection;

namespace WebApiClient
{
    /// <summary>
    /// 提供HttpApiClient代理搜索和实例化
    /// </summary>
    static class HttpApiClientProxy
    {
        /// <summary>
        /// 代理类型的构造器的参数类型
        /// </summary>
        private static readonly Type[] proxyTypeCtorArgTypes = new Type[] { typeof(IApiInterceptor), typeof(MethodInfo[]) };

        /// <summary>
        /// 接口类型与代理类型创建工厂缓存
        /// </summary>
        private static readonly ConcurrentCache<Type, Func<IApiInterceptor, MethodInfo[], HttpApiClient>> proxyTypeFactoryCache = new ConcurrentCache<Type, Func<IApiInterceptor, MethodInfo[], HttpApiClient>>();

        /// <summary>
        /// 搜索接口的代理类型并实例化
        /// </summary>
        /// <param name="interfaceType">接口类型</param>
        /// <param name="interceptor">拦截器</param>
        /// <exception cref="TypeLoadException"></exception>
        /// <returns></returns>
        public static HttpApiClient CreateInstance(Type interfaceType, IApiInterceptor interceptor)
        {
            var proxyTypeFactory = proxyTypeFactoryCache.GetOrAdd(interfaceType, type =>
            {
                var proxyType = FindProxyType(type);
                if (proxyType == null)
                {
                    return null;
                }
                return Lambda.CreateNewFunc<HttpApiClient, IApiInterceptor, MethodInfo[]>(proxyType);
            });

            if (proxyTypeFactory == null)
            {
                var assemblyName = typeof(HttpApiClientProxy).GetTypeInfo().Assembly.GetName();
                throw new TypeLoadException($"找不到接口{interfaceType}的代理类，请为接口所在项目重新使用Nuget安装{assemblyName.Name} {assemblyName.Version}");
            }

            var apiMethods = interfaceType.GetAllApiMethods();
            return proxyTypeFactory.Invoke(interceptor, apiMethods);
        }


        /// <summary>
        /// 查找代理类型
        /// </summary>
        /// <param name="interfaceType">接口类型</param>
        /// <returns></returns>
        private static Type FindProxyType(this Type interfaceType)
        {
            var allTypes = interfaceType.GetTypeInfo().Assembly.GetTypes();
            if (interfaceType.GetTypeInfo().IsGenericType == false)
            {
                return allTypes.FirstOrDefault(item => IsProxyType(interfaceType, item));
            }
            else
            {
                var definition = interfaceType.GetGenericTypeDefinition();
                var targetType = allTypes.FirstOrDefault(item => IsProxyType(definition, item));
                return targetType?.MakeGenericType(interfaceType.GenericTypeArguments);
            }
        }

        /// <summary>
        /// 返回target类型是否为代理类型
        /// </summary>
        /// <param name="interfaceType">接口类型</param>
        /// <param name="targetType">目标类型 </param>
        /// <returns></returns>
        private static bool IsProxyType(Type interfaceType, Type targetType)
        {
            if (targetType.Namespace != interfaceType.Namespace)
            {
                return false;
            }

            if (targetType.DeclaringType != interfaceType.DeclaringType)
            {
                return false;
            }

            const string prefix = "$";
            var proxyTypeName = $"{prefix}{interfaceType.Name}";
            return targetType.Name == proxyTypeName;
        }
    }
}
#endif