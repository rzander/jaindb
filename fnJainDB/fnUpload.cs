using System.IO;
using System.Net;
using System.Threading.Tasks;
using jaindb;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
//using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
//using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Enums;
using Microsoft.Extensions.Logging;
//using Microsoft.OpenApi.Models;
using Newtonsoft.Json;

namespace fnJainDB
{
    public static class fnUpload
    {
        [FunctionName("upload")]
        //[OpenApiOperation(operationId: "upload", tags: new[] { "Upload" }, Description = "Upload a JSON to JainDB")]
        //[OpenApiSecurity("function_key", SecuritySchemeType.ApiKey, Name = "code", In = OpenApiSecurityLocationType.Query)]
        //[OpenApiParameter(name: "id", In = ParameterLocation.Query, Required = true, Type = typeof(string), Description = "Unique Identfier of the Source (e.g. DeviceID)")]
        //[OpenApiRequestBody("JSON", typeof(string), Description = "JSON Body")]
        //[OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "text/plain", bodyType: typeof(string), Description = "JainDB hash of the uploaded JSON body")]
        public static async Task<IActionResult> Upload([HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req, ILogger log, ExecutionContext context)
        {
            log.LogInformation("C# HTTP trigger function 'upload' processed a request.");

            string id = req.Query["id"];

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            if (string.IsNullOrEmpty(requestBody) || string.IsNullOrEmpty(id))
                return new BadRequestResult();

            //Load JainDB Plugins...
            //jaindb.jDB.loadPlugins(context.FunctionAppDirectory);
            
            //Upload JainDB JSON...
            string sHash = await jDB.UploadFullAsync(requestBody, id);

            if (!string.IsNullOrEmpty(sHash))
                return new OkObjectResult(sHash);
            else
                return new BadRequestResult();
        }
    }
}

