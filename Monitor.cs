using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using System.Net.Http;

namespace gbelenky.monitor
{
    public static class Monitor
    {
        [FunctionName("RunOrchestrator")]
        public static async Task RunOrchestrator(
            [OrchestrationTrigger] IDurableOrchestrationContext context, ILogger logger)
        {
            await context.CallActivityAsync("StartDelay", null);
            return;
        }

        [FunctionName("StartDelay")]
        public static string StartDelay([ActivityTrigger] IDurableActivityContext context, ILogger log)
        {
            log.LogInformation($"Task started");

            var t = Task.Run(async delegate
                          {
                              await Task.Delay(10000);
                              return "Success";
                          });
            t.Wait();
            
            Console.WriteLine($"Task t Status: {t.Status}, Result: {t.Result}");
            return t.Result;
        }

        [FunctionName("StartLongRunClient")]
        public static async Task<HttpResponseMessage> HttpStart(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestMessage req,
            [DurableClient] IDurableOrchestrationClient starter,
            ILogger log)
        {
            // Function input comes from the request content.
            string instanceId = await starter.StartNewAsync("RunOrchestrator", null);

            log.LogInformation($"Started orchestration with ID = '{instanceId}'.");

            return starter.CreateCheckStatusResponse(req, instanceId);
        }
    }
}
