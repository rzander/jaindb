using System.Linq;
using System.Net;
using System.Threading.Tasks;
using jaindb;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
//using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
//using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Enums;
using Microsoft.Extensions.Logging;
//using Microsoft.OpenApi.Models;
using Newtonsoft.Json.Linq;

namespace fnJainDB
{
    public static class fnFull
    {
        [FunctionName("full")]
        //[OpenApiOperation(operationId: "Query", tags: new[] { "Query" }, Description = "Get data from JainDB")]
        //[OpenApiSecurity("function_key", SecuritySchemeType.ApiKey, Name = "code", In = OpenApiSecurityLocationType.Query)]
        //[OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "text/plain", bodyType: typeof(string), Description = "JSON result")]
        public static async Task<IActionResult> Full([HttpTrigger(AuthorizationLevel.Function, "get", Route = null)] HttpRequest req, ILogger log, ExecutionContext context)
        {
            log.LogInformation("C# HTTP trigger function 'full' processed a request.");

            string id = req.Query["id"];
            string sindex = req.Query["index"];
            string sBlockType = req.Query["BlockType"];

            if (string.IsNullOrEmpty(sBlockType))
                sBlockType = "INV";
            int index;
            if (!int.TryParse(sindex, out index))
                index = -1;

            //Load JainDB Plugins...
            //jaindb.jDB.loadPlugins(context.FunctionAppDirectory);

            JObject sRes = await jDB.GetFullAsync(id, index, sBlockType);

            if (sRes != null)
                return new OkObjectResult(sRes.ToString(Newtonsoft.Json.Formatting.None));
            else
                return new BadRequestResult();
        }
    }
}

