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

            int pollingInterval = 5;
            DateTime expiryTime = context.CurrentUtcDateTime.AddMinutes(1);

            await context.CallActivityAsync("StartAsyncJob", jobId);
            
            //var delay = context.CurrentUtcDateTime.AddMilliseconds(200);
            //await context.CreateTimer(delay, CancellationToken.None);
            
            while (context.CurrentUtcDateTime < expiryTime)
            {
                var jobStatus = await context.CallActivityAsync<string>("GetAsyncJobStatus", jobId);
                if (jobStatus == "Completed")
                {
                    // Perform an action when a condition is met.
                    log.LogInformation("Completed. All done");
                    break;
                }
                else if (jobStatus == "Queued")
                {
                    // Perform an action when a condition is met.
                    log.LogInformation("Queued. Prepared for start - please wait");
                }

                else if (jobStatus == "InProgress")
                {
                    // Perform an action when a condition is met.
                    log.LogInformation("InProgress. Already working - please wait");
                }

                // Orchestration sleeps until this time.
                var nextCheck = context.CurrentUtcDateTime.AddSeconds(pollingInterval);
                await context.CreateTimer(nextCheck, CancellationToken.None);
            }

            return;
        }

       [FunctionName("StartAsyncJob")]
        public static async Task StartAsyncJob([ActivityTrigger] string instanceId)
        {
            HttpClient httpClient = new HttpClient();
            var callString = $"http://localhost:7071/api/AsyncJobTrigger?instanceId={instanceId}";
            var response = await httpClient.GetAsync(callString);
            return ;
        }

        [FunctionName("GetAsyncJobStatus")]
        public static async Task<string> GetAsyncJobStatus([ActivityTrigger] string instanceId)
        {
            HttpClient httpClient = new HttpClient();
            
            var content = new StringContent("This is plain text!", Encoding.UTF8, "text/plain");
            
            var callString = $"http://localhost:7071/api/AsyncJobStatus?instanceId={instanceId}";
            string result = await httpClient.GetStringAsync(callString);

            return result;
        }

        [FunctionName("MonitorTrigger")]
        public static async Task<HttpResponseMessage> MonitorTrigger(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestMessage req,
            [DurableClient] IDurableOrchestrationClient starter,
            ILogger log)
        {
            // Function input comes from the request content.
            string jobId = req.RequestUri.Query.Split("=")[1];
            var instanceId = await starter.StartNewAsync("MonitorOrchestrator", null, jobId);

            log.LogInformation($"Started orchestration with ID = '{instanceId}'.");

            return starter.CreateCheckStatusResponse(req, instanceId);
        }

    }
}
