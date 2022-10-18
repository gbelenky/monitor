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
using Newtonsoft.Json.Linq;

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
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "job-start/{jobName}")] HttpRequestMessage req,
            [DurableClient] IDurableOrchestrationClient starter,
            [DurableClient] IDurableEntityClient client,
            string jobName,
            ILogger log)
        {
            string instanceId = $"job-{jobName}";
            // create mock jobId
            string jobId = Guid.NewGuid().ToString();

            var entityId = new EntityId(nameof(Job), jobId);
            await client.SignalEntityAsync(entityId, "SetJobName", jobName);

            if (!String.IsNullOrEmpty(instanceId))
            {

                // Check if an instance with the specified ID already exists or an existing one stopped running(completed/failed/terminated).
                var existingInstance = await starter.GetStatusAsync(instanceId);
                if (existingInstance == null
                || existingInstance.RuntimeStatus == OrchestrationRuntimeStatus.Completed
                || existingInstance.RuntimeStatus == OrchestrationRuntimeStatus.Failed
                || existingInstance.RuntimeStatus == OrchestrationRuntimeStatus.Terminated)
                {
                    // An instance with the specified ID doesn't exist or an existing one stopped running, create one.
                    await starter.StartNewAsync("AsyncJobOrchestrator", instanceId);
                    log.LogInformation($"Started AsyncJobOrchestrator with jobName = '{jobName}'");
                    
                    JobResult result = new JobResult()
                    {
                        JobId = jobId,
                        JobName = jobName,
                        JobStatus = "Initializing"
                    };
                    return new HttpResponseMessage(HttpStatusCode.Accepted)
                    {
                        Content = new StringContent(JsonConvert.SerializeObject(result))
                    };
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

        [FunctionName("AsyncJobStatus")]
        public static async Task<HttpResponseMessage> AsyncJobStatus(
           [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "job-status/{jobId}")] HttpRequestMessage req,
            [DurableClient] IDurableOrchestrationClient starter,
            [DurableClient] IDurableEntityClient client,
            string jobId,
            ILogger log)
        {
            var entityId = new EntityId(nameof(Job), jobId);
            var entityState = await client.ReadEntityStateAsync<Job>(entityId);
            string jobName = entityState.EntityState.GetJobName();

            string instanceId = $"job-{jobName}";
            DurableOrchestrationStatus orchStatus = await starter.GetStatusAsync(instanceId);

            JobResult result = new JobResult()
            {
                JobId = jobId,
                JobName = jobName,
                JobStatus = orchStatus.CustomStatus.ToString()
            };
            HttpResponseMessage httpResponseMessage = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonConvert.SerializeObject(result))
            };
            log.LogTrace($"AsyncJobStatus: current status for jobName '{jobName}' jobId '{jobId}' is '{orchStatus.CustomStatus.ToString()}'");
            return httpResponseMessage;
        }
    }

    [JsonObject(MemberSerialization.OptIn)]
    public class Job
    {
        [JsonProperty("jobName")]
        public string JobName { get; set; }
        public void SetJobName(string jobName) => this.JobName = jobName;
        public string GetJobName() => this.JobName;


        [FunctionName(nameof(Job))]
        public static Task Run([EntityTrigger] IDurableEntityContext ctx)
            => ctx.DispatchAsync<Job>();
    }

    public class JobResult
    {
        [JsonProperty("jobId")]
        public string JobId { get; set; }
        [JsonProperty("jobName")]
        public string JobName { get; set; }
        [JsonProperty("jobStatus")]
        public string JobStatus { get; set; }
    }
}
