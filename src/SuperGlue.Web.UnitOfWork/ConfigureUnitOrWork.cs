using System;
using System.Collections.Generic;
using SuperGlue.Web.Configuration;

namespace SuperGlue.Web.UnitOfWork
{
    public class ConfigureUnitOrWork : ISetupConfigurations
    {
        public IEnumerable<ConfigurationSetupResult> Setup()
        {
            yield return new ConfigurationSetupResult("superglue.UnitOfWork.Configure", environment => environment.Get<Action<Type>>("superglue.Container.RegisterAll")(typeof(ISuperGlueUnitOfWork)), "superglue.ContainerSetup");
        }

        public void Shutdown(IDictionary<string, object> applicationData)
        {

        }
    }
}