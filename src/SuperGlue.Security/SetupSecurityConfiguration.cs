﻿using System.Collections.Generic;
using System.Threading.Tasks;
using SuperGlue.Configuration;
using SuperGlue.Security.Authentication;
using SuperGlue.Security.Authorization;

namespace SuperGlue.Security
{
    public class SetupSecurityConfiguration : ISetupConfigurations
    {
        public IEnumerable<ConfigurationSetupResult> Setup(string applicationEnvironment)
        {
            yield return new ConfigurationSetupResult("superglue.SecuritySetup", environment =>
            {
                environment.RegisterAll(typeof(IAuthenticationTokenSource));
                environment.RegisterAll(typeof(IFindRequiredAuthorizationInformationFromRequest));
                environment.RegisterAllClosing(typeof(IValidateAuthorizationInformation<>));
                environment.RegisterAll(typeof(IAuthenticationStrategy));
                environment.RegisterAllClosing(typeof(IAuthenticationStrategy<>));

                environment.RegisterTransient(typeof(IHasher), (x, y) => new DefaultHasher(y.GetSettings<HasherSettings>()));
                environment.RegisterTransient(typeof(IAuthenticationService), typeof(DefaultAuthenticationService));

                return Task.CompletedTask;
            }, "superglue.ContainerSetup");
        }
    }
}