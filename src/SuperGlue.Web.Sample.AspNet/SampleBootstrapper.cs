﻿using SuperGlue.Configuration;
using SuperGlue.Diagnostics;
using SuperGlue.ExceptionManagement;
using SuperGlue.RequestBranching;
using SuperGlue.Security.Authorization;
using SuperGlue.StructureMap;
using SuperGlue.UnitOfWork;
using SuperGlue.Web.Endpoints;
using SuperGlue.Web.Http;
using SuperGlue.Web.ModelBinding;
using SuperGlue.Web.Output;
using SuperGlue.Web.Output.Spark;
using SuperGlue.Web.Routing.Superscribe;
using SuperGlue.Web.Routing.Superscribe.Policies.MethodEndpoint;
using SuperGlue.Web.Validation;
using SuperGlue.Web.Validation.InputValidation;

namespace SuperGlue.Web.Sample.AspNet
{
    public class SampleBootstrapper : SuperGlueBootstrapper
    {
        protected override void Configure()
        {
            this.UseRoutePolicy(new MethodEndpointRoutePolicy(new DefaultEndpointBuilder()));

            this.ConfigureOutput()
                .When(x => x.GetRequest().Headers.Accept.Contains("text/html")).UseSpark()
                .When(x => true).UseRenderer(new RenderOutputAsJson());

            AddChain("chains.Partials", app =>
            {
                app
                    .Use<HandleExceptions>()
                    .Use<AuthorizeRequest>(new AuthorizeRequestOptions().WithAuthorizer(new TestAuthorizer()))
                    .Use<ExecuteEndpoint>()
                    .Use<RenderOutput>();
            });

            AddChain("chains.Web", app =>
            {
                var container = Environment.GetContainer();
                var diagnosticsManager = container.GetInstance<IManageDiagnosticsInformation>();

                app.Use<Diagnose>(new DiagnoseOptions(diagnosticsManager, "Urls", x => x.GetRequest().Uri.ToString()))
                    .Use<RedirectToCorrectUrl>(new RedirectToCorrectUrlOptions((url, environment) => url.ToLower()))
                    .Use<BranchRequest>(new BranchRequestConfiguration()
                        .AddCase(y => y.GetException() != null, app
                            .New()
                            .Use<SetStatusCode>(500)
                            .Use<RollbackUnitOfWork>()
                            .Use<HandledExceptionMiddleware>()
                            .Build())
                        .AddCase(y => y.HasAuthorizationFailed(), app
                            .New()
                            .Use<SetStatusCode>(401)
                            .Use<HandleUnauthorizedMiddleware>()
                            .Build())
                        .AddCase(y => !y.GetValidationResult().IsValid, app
                            .New()
                            .Use<HandleValidationErrorMiddleware>()
                            .Build())
                        .AddCase(y => y.GetRouteInformation().RoutedTo == null, app
                            .New()
                            .Use<SetStatusCode>(404)
                            .Use<HandleNotFoundMiddleware>()
                            .Build()))
                    .Use<HandleExceptions>()
                    .Use<NestedStructureMapContainer>(container)
                    .Use<BindModels>(container.GetInstance<IModelBinderCollection>())
                    .Use<RouteUsingSuperscribe>(new RouteUsingSuperscribeOptions(Environment.GetRouteEngine()))
                    .Use<HandleUnitOfWork>()
                    .Use<AuthorizeRequest>(new AuthorizeRequestOptions().WithAuthorizer(new TestAuthorizer()))
                    .Use<ValidateRequest>(new ValidateRequestOptions().UsingValidator(new ValidateRequestInput()))
                    .Use<ExecuteEndpoint>()
                    .Use<RenderOutput>();
            });
        }
    }
}