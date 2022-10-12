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
using System.Threading;
using System.Net;

namespace gbelenky.monitor
{
    public static class AsyncJob
    {
        [FunctionName("AsyncJobOrchestrator")]
        public static async Task AsyncJobOrchestrator(
            [OrchestrationTrigger] IDurableOrchestrationContext context, ILogger logger)
        {

            context.SetCustomStatus("Queued");
            DateTime queuedTime = context.CurrentUtcDateTime.AddSeconds(10);
            await context.CreateTimer(queuedTime, CancellationToken.None);

            context.SetCustomStatus("InProgress");
            DateTime inProgressTime = context.CurrentUtcDateTime.AddSeconds(10);
            await context.CreateTimer(inProgressTime, CancellationToken.None);

            context.SetCustomStatus("Completed");

            return;
        }

        [FunctionName("AsyncJobTrigger")]
        public static async Task<HttpResponseMessage> AsyncJobTrigger(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestMessage req,
            [DurableClient] IDurableOrchestrationClient starter,
            ILogger log)
        {
            // Function input comes from the request content.
            string instanceId = req.RequestUri.Query.Split("=")[1];
            await starter.StartNewAsync("AsyncJobOrchestrator", instanceId);

            log.LogInformation($"Started orchestration with ID = '{instanceId}'.");

            return starter.CreateCheckStatusResponse(req, instanceId);
        }

        [FunctionName("AsyncJobStatus")]
        public static async Task<HttpResponseMessage> AsyncJobStatus(
       [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestMessage req,
           [DurableClient] IDurableOrchestrationClient client, ILogger log)
        {
            string instanceId = req.RequestUri.Query.Split("=")[1];
            DurableOrchestrationStatus orchStatus = await client.GetStatusAsync(instanceId);
            HttpResponseMessage httpResponseMessage = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(orchStatus.CustomStatus.ToString())
            };

            return httpResponseMessage;
        }
    }
}
