﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HtmlTags.Conventions.Elements;
using HtmlTags.Conventions.Elements.Builders;
using HtmlTags.Reflection;
using SuperGlue.Configuration;
using SuperGlue.Configuration.Ioc;
using SuperGlue.Web.ModelBinding;
using SuperGlue.Web.Output.Html.Autocomplete;

namespace SuperGlue.Web.Output.Html
{
    public class SetupHtmlConfiguration : ISetupConfigurations
    {
        public IEnumerable<ConfigurationSetupResult> Setup(string applicationEnvironment)
        {
            yield return new ConfigurationSetupResult("superglue.HtmlSetup", environment =>
            {
                environment.AlterSettings<IocConfiguration>(x => x.Register(typeof(IElementGenerator<>), (y, z) => environment.GetHtmlConventionSettings().ElementGeneratorFor(y.GenericTypeArguments.First())));

                environment.CreateRoute("/_autocomplete/{Slug}?s={Search}", typeof(Search).GetMethod("SearchQuery", new[] { typeof(SearchQueryInput) }), new Dictionary<Type, Func<object, IDictionary<string, object>>>
                {
                    {typeof(SearchQueryInput), x => new Dictionary<string, object>
                    {
                        {"Slug", ((SearchQueryInput)x).Slug},
                        {"Search", ((SearchQueryInput)x).Search}
                    }}
                }, "GET");

                environment.AlterSettings<HtmlConventionSettings>(x =>
                {
                    x.Editors.BuilderPolicy<CheckboxBuilder>();

                    x.Editors.Always.BuildBy<TextboxBuilder>();

                    x.Editors.Modifier<AddNameModifier>();

                    x.Editors.If(y => y.Accessor.HasAttribute<ValueOfAttribute>()).BuildBy<ValueObjectDropdownBuilder>();

                    x.Editors.IfPropertyIs<bool>().ModifyWith(y => y.CurrentTag.Attr("type", "checkbox").Attr("value", "true"));

                    x.Editors.IfPropertyIs<PostedFile>().ModifyWith(y => y.CurrentTag.Attr("type", "file"));

                    x.Editors.If(y => y.Accessor.HasAttribute<AutocompleteAttribute>()).ModifyWith(y =>
                    {
                        var attribute = y.Accessor.GetAttribute<AutocompleteAttribute>();
                        y.OriginalTag.AddClass("autocomplete");
                        y.OriginalTag.Data("name", y.ElementId);

                        if (!string.IsNullOrEmpty(attribute.Remote))
                        {
                            y.OriginalTag.Data("autocomplete-remote", environment.RouteTo(new SearchQueryInput
                            {
                                Search = "_QUERY_",
                                Slug = attribute.Remote.ToLower()
                            }).Replace("_QUERY_", "%QUERY"));
                        }
                    });

                    x.Displays.Always.BuildBy<SpanDisplayBuilder>();

                    x.Labels.Always.BuildBy<DefaultLabelBuilder>();

                    x.Forms.Always.BuildBy<FormBuilder>();

                    x.Forms.If(y => y.Accessor.OwnerType.GetProperties().Any(z => typeof(PostedFile).IsAssignableFrom(z.PropertyType))).ModifyWith(y => y.CurrentTag.Attr("enctype", "multipart/form-data"));
                });

                return Task.CompletedTask;
            }, "superglue.RoutingImplimentationSetup");
        }
    }
}