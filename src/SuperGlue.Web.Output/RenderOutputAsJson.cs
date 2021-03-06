using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace SuperGlue.Web.Output
{
    public class RenderOutputAsJson : IRenderOutput
    {
        public async Task<OutputRenderingResult> Render(IDictionary<string, object> environment)
        {
            var output = environment.GetOutput();

            if (output == null)
                return null;

            var serialized = JsonConvert.SerializeObject(output);

            return new OutputRenderingResult(await serialized.ToStream().ConfigureAwait(false), "application/json");
        }
    }
}