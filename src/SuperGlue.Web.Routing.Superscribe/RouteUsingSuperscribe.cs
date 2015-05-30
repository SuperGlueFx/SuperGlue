﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Superscribe.Engine;

namespace SuperGlue.Web.Routing.Superscribe
{
    using AppFunc = Func<IDictionary<string, object>, Task>;

    public class RouteUsingSuperscribe
    {
        private readonly AppFunc _next;

        public RouteUsingSuperscribe(AppFunc next)
        {
            if (next == null)
                throw new ArgumentNullException("next");

            _next = next;
        }

        public async Task Invoke(IDictionary<string, object> environment)
        {
            var path = environment.GetRequest().Path;
            var method = environment.GetRequest().Method;

            var routeEngine = environment.GetRouteEngine();

            var routeData = new RouteData { Environment = environment };
            var walker = routeEngine.Walker();
            var data = walker.WalkRoute(path, method, routeData);

            environment.SetRouteDestination(data.Response, (IDictionary<string, object>)data.Parameters);

            await _next(environment);
        }
    }
}