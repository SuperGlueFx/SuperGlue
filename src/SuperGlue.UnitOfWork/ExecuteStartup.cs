using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SuperGlue.Configuration;

namespace SuperGlue.UnitOfWork
{
    public class ExecuteStartup
    {
        private readonly Func<IDictionary<string, object>, Task> _next;

        public ExecuteStartup(Func<IDictionary<string, object>, Task> next)
        {
            if (next == null)
                throw new ArgumentNullException(nameof(next));

            _next = next;
        }

        public async Task Invoke(IDictionary<string, object> environment)
        {
            var applicationTasks = environment.ResolveAll<IApplicationTask>().ToList();

            foreach (var applicationTask in applicationTasks)
                await applicationTask.Start(environment).ConfigureAwait(false);

            await _next(environment).ConfigureAwait(false);
        }
    }
}