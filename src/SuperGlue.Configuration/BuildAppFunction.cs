﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using SuperGlue.Configuration.Ioc;

namespace SuperGlue.Configuration
{
    using AppFunc = Func<IDictionary<string, object>, Task>;

    public class BuildAppFunction : IBuildAppFunction
    {
        private readonly ICollection<MiddlewareWithArgs> _middleware = new List<MiddlewareWithArgs>();
        private readonly IDictionary<string, object> _environment;
        private readonly string _chainName;
        private readonly IResolveServices _resolveServices;

        public BuildAppFunction(IDictionary<string, object> environment, string chainName)
        {
            _environment = environment;
            _chainName = chainName;
            _resolveServices = environment.Get<IResolveServices>(SetupIocConfiguration.ServiceResolverKey);
        }

        public IBuildAppFunction Use<TMiddleware>(params object[] args)
        {
            var wrappers = FindWrappersFor(typeof(TMiddleware));

            foreach (var wrapper in wrappers)
                _middleware.Add(wrapper);

            _middleware.Add(new MiddlewareWithArgs(typeof(TMiddleware), args));

            return this;
        }

        public AppFunc Build()
        {
            var wrappers = FindWrappersFor(typeof(ConfigureEnvironment));

            var list = wrappers.ToList();

            list.Add(new MiddlewareWithArgs(typeof(ConfigureEnvironment), new object[] { new ConfigureEnvironmentOptions(_environment, _chainName) }));

            list.AddRange(_middleware);

            list.Reverse();

            AppFunc lastFunc = x => Task.CompletedTask;

            foreach (var item in list)
            {
                var args = new List<object>
                {
                    lastFunc
                };

                args.AddRange(item.Args);

                var constructor = item.Type.GetConstructor(args.Select(x => x.GetType()).ToArray());

                if (constructor == null)
                {
                    _environment.Log("Can't find constructor with {0} args for type: {1}. Arguments: {2}", LogLevel.Error, args.Count, item.Type.FullName, string.Join(", ", args.Select(x => x.GetType().FullName)));
                    continue;
                }

                var instance = constructor.Invoke(args.ToArray());

                var method = instance.GetType().GetMethod("Invoke", new[] { typeof(IDictionary<string, object>) });

                var parameter = Expression.Parameter(typeof(IDictionary<string, object>));

                lastFunc = Expression.Lambda<AppFunc>(Expression.Call(Expression.Constant(instance), method, parameter), parameter).Compile();
            }

            return lastFunc;
        }

        public IBuildAppFunction New()
        {
            return new BuildAppFunction(_environment, _chainName);
        }

        private IEnumerable<MiddlewareWithArgs> FindWrappersFor(Type middleWareType)
        {
            return from type in FindInheritedTypes(middleWareType)
                   from wrapper in _resolveServices.ResolveAll(typeof(IWrapMiddleware<>).MakeGenericType(type))
                   let optionsConstructor = typeof(WrapMiddlewareOptions<>).MakeGenericType(type).GetConstructor(new[] { typeof(IWrapMiddleware<>).MakeGenericType(type), typeof(Type) })
                   where optionsConstructor != null
                   select new MiddlewareWithArgs(typeof(WrapMiddleware<>).MakeGenericType(type), new[]
                        {
                            optionsConstructor.Invoke(new[] {wrapper, middleWareType})
                        });
        }

        private static IEnumerable<Type> FindInheritedTypes(Type middleWareType)
        {
            yield return middleWareType;

            foreach (var @interface in middleWareType.GetInterfaces())
                yield return @interface;

            var baseType = middleWareType.BaseType;

            while (baseType != null)
            {
                yield return baseType;
                baseType = baseType.BaseType;
            }
        }

        public class MiddlewareWithArgs
        {
            public MiddlewareWithArgs(Type type, object[] args)
            {
                Type = type;
                Args = args;
            }

            public Type Type { get; }
            public object[] Args { get; }
        }
    }
}