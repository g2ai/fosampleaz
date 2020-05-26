using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Net.Http;
using System.Net.Http.Headers; 
using Microsoft.Azure.WebJobs.Host;
using System.Text;
using Newtonsoft.Json.Linq;
using System.Net;
using RestSharp;
using Microsoft.Extensions.Configuration;

namespace FunctionAppFailover
{
    public static class InvokeFailover
    {
        static RestClient client = null; 

        private static string GetBearerToken(ConfigWrapper config)
        {
            client = new RestClient("https://login.microsoftonline.com/72f988bf-86f1-41af-91ab-2d7cd011db47/oauth2/token");
            client.Timeout = -1;
            var request = new RestRequest(Method.POST);
            request.AddHeader("Cookie", "stsservicecookie=estsfd; fpc=AlsSOR2lIsxOqFLubHOBxlufnQOrAQAAAHB6VdYOAAAA; x-ms-gateway-slice=estsfd");
            request.AlwaysMultipartFormData = true;
            request.AddParameter("grant_type", config.GrantType);
            request.AddParameter("client_id", config.ClientId);
            request.AddParameter("client_secret", config.ClientSecret);
            request.AddParameter("resource", config.Resource);
            IRestResponse response = client.Execute(request);
            Console.WriteLine(response.Content);
            dynamic jo = JsonConvert.DeserializeObject(response.Content);
            string token = jo.access_token.Value;
            if (!string.IsNullOrEmpty(token))
                return token;
            else
                return null;
        }

        private static async Task<string> TriggerFailover(string token, string name)
        {
            var client2 = new HttpClient();

            if (name == "eastus")
            {
                client2.BaseAddress = new Uri("https://management.azure.com/subscriptions/86306f52-a93a-48f2-a3f2-d34b242a37c9/resourceGroups/rgDrDemoEUS/providers/Microsoft.Sql/servers/productservereus/failoverGroups/productdbgroup/failover?api-version=2015-05-01-preview");
            }
            else
            {
                client2.BaseAddress = new Uri("https://management.azure.com/subscriptions/86306f52-a93a-48f2-a3f2-d34b242a37c9/resourceGroups/rgDrDemo/providers/Microsoft.Sql/servers/productserversea/failoverGroups/productdbgroup/failover?api-version=2015-05-01-preview");
            }

            var request = new HttpRequestMessage(HttpMethod.Post, "");
            request.Headers.Add("Authorization", "Bearer " + token);
            var response = await client2.SendAsync(request);

            return response.Content.ReadAsStringAsync().Result.ToString();
        }

        [FunctionName("InvokeFailover")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            var currentDirectory = "/home/site/wwwroot";
            bool isLocal = string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID"));
            if (isLocal)
            {
                currentDirectory = Directory.GetCurrentDirectory();
            }


            ConfigWrapper config = new ConfigWrapper(new ConfigurationBuilder()
                .SetBasePath(currentDirectory)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build());

            string name = req.Query["serverName"].ToString().ToLower();
            if (name == null)
            {
                name = "southeastasia";
            }

            string token = GetBearerToken(config);

            string rgResult = await TriggerFailover(token, name);

            return new OkObjectResult(rgResult);
        }
    }
}
