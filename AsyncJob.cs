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
            string jobId = req.RequestUri.Query.Split("=")[1];

            if (!String.IsNullOrEmpty(jobId))
            {

                // Check if an instance with the specified ID already exists or an existing one stopped running(completed/failed/terminated).
                var existingInstance = await starter.GetStatusAsync(jobId);
                if (existingInstance == null
                || existingInstance.RuntimeStatus == OrchestrationRuntimeStatus.Completed
                || existingInstance.RuntimeStatus == OrchestrationRuntimeStatus.Failed
                || existingInstance.RuntimeStatus == OrchestrationRuntimeStatus.Terminated)
                {
                    // An instance with the specified ID doesn't exist or an existing one stopped running, create one.
                    await starter.StartNewAsync("AsyncJobOrchestrator", jobId);
                    log.LogInformation($"Started AsyncJobOrchestrator with ID = '{jobId}'.");
                    return starter.CreateCheckStatusResponse(req, jobId);
                }
                else
                {
                    // An instance with the specified ID exists or an existing one still running, don't create one.
                    return new HttpResponseMessage(HttpStatusCode.Conflict)
                    {
                        Content = new StringContent($"An job with ID '{jobId}' already exists."),
                    };
                }
            }
            else
            {
                return new HttpResponseMessage(HttpStatusCode.UnprocessableEntity)
                {
                    Content = new StringContent($"Please provide jobId in your request payload"),
                };

            }
        }

        [FunctionName("AsyncJobStatus")]
        public static async Task<HttpResponseMessage> AsyncJobStatus(
       [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestMessage req,
           [DurableClient] IDurableOrchestrationClient client, ILogger log)
        {
            string jobId = req.RequestUri.Query.Split("=")[1];
            DurableOrchestrationStatus orchStatus = await client.GetStatusAsync(jobId);

            HttpResponseMessage httpResponseMessage = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(orchStatus.CustomStatus.ToString())
            };
            log.LogTrace($"AsyncJobStatus: current status for job {jobId} is {orchStatus.CustomStatus.ToString()}");
            return httpResponseMessage;
        }
    }
}
