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
            string jobName = context.GetInput<string>();
            //string jobName = context.InstanceId;
            int pollingInterval = 1;
            DateTime expiryTime = context.CurrentUtcDateTime.AddMinutes(1);

            string jobId = await context.CallActivityAsync<string>("StartAsyncJob", jobName);

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
        public static async Task<string> StartAsyncJob([ActivityTrigger] string jobName)
        {
            HttpClient httpClient = new HttpClient();
            string callString = $"http://localhost:7071/api/job-start/{jobName}";
            string jobId = await httpClient.GetStringAsync(callString);

            return jobId;
        }

        [FunctionName("GetAsyncJobStatus")]
        public static async Task<string> GetAsyncJobStatus([ActivityTrigger] string jobId, ILogger log)
        {
            HttpClient httpClient = new HttpClient();

            string callString = $"http://localhost:7071/api/job-status/{jobId}";
            string jobResultStr = await httpClient.GetStringAsync(callString);
            JobResult jobResult = JsonConvert.DeserializeObject<JobResult>(jobResultStr);
            string jobStatus = jobResult.JobStatus;
            if ((jobStatus == "Queued") || (jobStatus == "InProgress") || (jobStatus == "Completed"))
            {
                log.LogTrace($"Monitor: current status for jobName '{jobResult.JobName}' jobId '{jobId}' is '{jobStatus}' ");
            }
            return jobStatus;
        }

        [FunctionName("MonitorTrigger")]
        public static async Task<HttpResponseMessage> MonitorTrigger(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "monitor-job/{jobName}")] HttpRequestMessage req,
            [DurableClient] IDurableOrchestrationClient starter,
            string jobName,
            ILogger log)
        {
            string instanceId = $"monitor-{jobName}";
            var existingInstance = await starter.GetStatusAsync(instanceId);
            if (!String.IsNullOrEmpty(instanceId))
            {
                if (existingInstance == null
                || existingInstance.RuntimeStatus == OrchestrationRuntimeStatus.Completed
                || existingInstance.RuntimeStatus == OrchestrationRuntimeStatus.Failed
                || existingInstance.RuntimeStatus == OrchestrationRuntimeStatus.Terminated)
                {
                    // An instance with the specified ID doesn't exist or an existing one stopped running, create one.
                    await starter.StartNewAsync("MonitorOrchestrator", instanceId, jobName);
                    log.LogInformation($"Started MonitorOrchestrator with Id = '{instanceId}'.");
                    return starter.CreateCheckStatusResponse(req, instanceId);
                }
                else
                {
                    // An instance with the specified ID exists or an existing one still running, don't create one.
                    return new HttpResponseMessage(HttpStatusCode.Conflict)
                    {
                        Content = new StringContent($"An instance with Id '{instanceId}' already exists."),
                    };
                }
            }
            else
            {
                return new HttpResponseMessage(HttpStatusCode.UnprocessableEntity)
                {
                    Content = new StringContent($"Please provide instance Id in your request payload"),
                };

            }
        }

    }
}
