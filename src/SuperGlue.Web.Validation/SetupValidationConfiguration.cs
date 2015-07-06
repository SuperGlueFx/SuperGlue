﻿using System.Collections.Generic;
using System.Threading.Tasks;
using SuperGlue.Configuration;
using SuperGlue.Web.Validation.InputValidation;

namespace SuperGlue.Web.Validation
{
    public class SetupValidationConfiguration : ISetupConfigurations
    {
        public IEnumerable<ConfigurationSetupResult> Setup(string applicationEnvironment)
        {
            yield return new ConfigurationSetupResult("superglue.ValidationSetup", environment =>
            {
                environment.RegisterAllClosing(typeof(IValidateInput<>));
                environment.RegisterAll(typeof(IValidateRequest));
            }, "superglue.Container");
        }

        public Task Shutdown(IDictionary<string, object> applicationData)
        {
            return Task.CompletedTask;
        }

        public Task Configure(SettingsConfiguration configuration)
        {
            return Task.CompletedTask;
        }
    }
}