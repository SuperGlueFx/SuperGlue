using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SuperGlue.Configuration;
using SuperGlue.Configuration.Ioc;

namespace SuperGlue.UnitOfWork
{
    using AppFunc = Func<IDictionary<string, object>, Task>;

    public class ConfigureUnitOfWork : ISetupConfigurations
    {
        public IEnumerable<ConfigurationSetupResult> Setup(string applicationEnvironmente)
        {
            yield return new ConfigurationSetupResult("superglue.UnitOfWork.Configure", environment =>
            {
                environment.AlterSettings<IocConfiguration>(x => x.Scan(typeof(ISuperGlueUnitOfWork)).Scan(typeof(IApplicationTask)));

                environment.SubscribeTo(ConfigurationEvents.BeforeApplicationStart, async x =>
                {
                    var requestEnvironment = new Dictionary<string, object>();

                    foreach (var item in x)
                        requestEnvironment[item.Key] = item.Value;

                    var startupChain = await GetApplicationStartupChain(x).ConfigureAwait(false);

                    await startupChain(requestEnvironment).ConfigureAwait(false);
                });

                environment.SubscribeTo(ConfigurationEvents.AfterApplicationShutDown, async x =>
                {
                    var requestEnvironment = new Dictionary<string, object>();

                    foreach (var item in x)
                        requestEnvironment[item.Key] = item.Value;

                    var shutdownChain = await GetApplicationShutdownChain(x).ConfigureAwait(false);

                    await shutdownChain(requestEnvironment).ConfigureAwait(false);
                });

                return Task.CompletedTask;
            }, "superglue.ContainerSetup");
        }

        private static Task<AppFunc> GetApplicationStartupChain(IDictionary<string, object> environment)
        {
            return environment.GetNamedChain("chains.StartupApplication", x => x.Use<HandleUnitOfWork>(new HandleUnitOfWorkOptions(true)).Use<ExecuteStartup>());
        }

        private static Task<AppFunc> GetApplicationShutdownChain(IDictionary<string, object> environment)
        {
            return environment.GetNamedChain("chains.ShutdownApplication", x => x.Use<HandleUnitOfWork>(new HandleUnitOfWorkOptions(true)).Use<ExecuteShutdown>());
        }
    }
}