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
using System.Text;

namespace gbelenky.monitor
{
    public static class Monitor
    {
        [FunctionName("MonitorOrchestrator")]
        public static async Task MonitorOrchestrator(
            [OrchestrationTrigger] IDurableOrchestrationContext context, ILogger log)
        {
            string jobId = context.GetInput<string>();

            int pollingInterval = 1;
            DateTime expiryTime = context.CurrentUtcDateTime.AddMinutes(1);

            await context.CallActivityAsync("StartAsyncJob", jobId);

            while (context.CurrentUtcDateTime < expiryTime)
            {
                var jobStatus = await context.CallActivityAsync<string>("GetAsyncJobStatus", jobId);
                // Orchestration sleeps until this time.
                var nextCheck = context.CurrentUtcDateTime.AddSeconds(pollingInterval);
                Task timeoutTask = context.CreateTimer(nextCheck, CancellationToken.None);
            }
            return;
        }

        [FunctionName("StartAsyncJob")]
        public static async Task StartAsyncJob([ActivityTrigger] string jobId)
        {
            HttpClient httpClient = new HttpClient();
            var callString = $"http://localhost:7071/api/AsyncJobTrigger?jobId={jobId}";
            var response = await httpClient.GetAsync(callString);
            return;
        }

        [FunctionName("GetAsyncJobStatus")]
        public static async Task<string> GetAsyncJobStatus([ActivityTrigger] string jobId, ILogger log)
        {
            HttpClient httpClient = new HttpClient();

            var callString = $"http://localhost:7071/api/AsyncJobStatus?jobId={jobId}";
            string jobStatus = await httpClient.GetStringAsync(callString);
            if ((jobStatus == "Queued") || (jobStatus == "InProgress") || (jobStatus == "Completed"))
                {
                    log.LogTrace($"Monitor: current status for job {jobId} is {jobStatus}");
                }
            return jobStatus;
        }

        [FunctionName("MonitorTrigger")]
        public static async Task<HttpResponseMessage> MonitorTrigger(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestMessage req,
            [DurableClient] IDurableOrchestrationClient starter,
            ILogger log)
        {
            // Function input comes from the request content.
            string jobId = req.RequestUri.Query.Split("=")[1];
            string monitorJobId = $"Monitor-{jobId}";

            var existingInstance = await starter.GetStatusAsync(monitorJobId);
            if (existingInstance == null
            || existingInstance.RuntimeStatus == OrchestrationRuntimeStatus.Completed
            || existingInstance.RuntimeStatus == OrchestrationRuntimeStatus.Failed
            || existingInstance.RuntimeStatus == OrchestrationRuntimeStatus.Terminated)
            {
                // An instance with the specified ID doesn't exist or an existing one stopped running, create one.
                await starter.StartNewAsync("MonitorOrchestrator", monitorJobId, jobId);
                log.LogInformation($"Started MonitorOrchestrator with ID = '{monitorJobId}'.");
                return starter.CreateCheckStatusResponse(req, monitorJobId);
            }
            else
            {
                // An instance with the specified ID exists or an existing one still running, don't create one.
                return new HttpResponseMessage(HttpStatusCode.Conflict)
                {
                    Content = new StringContent($"An instance with ID '{jobId}' already exists."),
                };
            }
        }

    }
}
