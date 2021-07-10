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
    public static class fnQuery
    {
        [FunctionName("query")]
        //[OpenApiOperation(operationId: "Query", tags: new[] { "Query" }, Description = "Get data from JainDB")]
        //[OpenApiSecurity("function_key", SecuritySchemeType.ApiKey, Name = "code", In = OpenApiSecurityLocationType.Query)]
        //[OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "text/plain", bodyType: typeof(string), Description = "JSON result")]
        public static async Task<IActionResult> Query([HttpTrigger(AuthorizationLevel.Function, "get", Route = null)] HttpRequest req, ILogger log, ExecutionContext context)
        {
            log.LogInformation("C# HTTP trigger function 'query' processed a request.");

            //Load JianDB Plugins...
            //jaindb.jDB.loadPlugins(context.FunctionAppDirectory);

            string sQuery = (req).QueryString.ToString();
            if (string.IsNullOrEmpty(sQuery))
            {
                sQuery = "OS";
            }
            var query = QueryHelpers.ParseQuery(sQuery);
#if DEBUG
            JArray sRes = await jDB.QueryAsync(string.Join(";", query.Where(t => string.IsNullOrEmpty(t.Value)).Select(t => t.Key).ToList()), query.FirstOrDefault(t => t.Key.ToLower() == "$select").Value, query.FirstOrDefault(t => t.Key.ToLower() == "$exclude").Value, query.FirstOrDefault(t => t.Key.ToLower() == "$where").Value, true);
#endif
#if RELEASE
            JArray sRes = await jDB.QueryAsync(string.Join(";", query.Where(t => string.IsNullOrEmpty(t.Value)).Select(t => t.Key).ToList()), query.FirstOrDefault(t => t.Key.ToLower() == "$select").Value, query.FirstOrDefault(t => t.Key.ToLower() == "$exclude").Value, query.FirstOrDefault(t => t.Key.ToLower() == "$where").Value);
#endif
            if (sRes != null)
                return new OkObjectResult(sRes.ToString( Newtonsoft.Json.Formatting.None));
            else
                return new BadRequestResult();
        }
    }
}

