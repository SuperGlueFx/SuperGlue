using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SuperGlue.Web.Output
{
    public class RenderStringOutput : IRenderOutput
    {
        public async Task<OutputRenderingResult> Render(IDictionary<string, object> environment)
        {
            var output = environment.GetOutput() as string ?? "";

            return new OutputRenderingResult(await output.ToStream().ConfigureAwait(false), environment.GetRequest().Headers.Accept.Split(',').Select(x => x.Trim()).FirstOrDefault());
        }
    }
}