using System.Collections.Generic;
using System.Threading.Tasks;
using SuperGlue.HttpClient;

namespace SuperGlue.ApiDiscovery
{
    public interface IExecuteApiRequests
    {
        Task<ApiResource> ExecuteQuery(ApiDefinition definition, IEnumerable<IApiLinkTravelInstruction> instructions, IDictionary<string, object> data);
        Task<IHttpResponse> ExecuteForm(ApiResource resource, IFormTravelInstruction travelInstruction, IDictionary<string, object> data);
    }
}