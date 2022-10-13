# Monitoring long running async HTTP REST requests
You might need to monitor long running async HTTP REST requests. Durable Functions are the best fit for the monitoring solutions since they can run over a long time period, being even able to implement perpetual operations.   

This example consists of two orchestrators.
 [AsyncJobOrchestrator](AsyncJob.cs) emulates the async HTTP run with AsyncJobTrigger and also provides the status endpoint AsyncJobStatus.

It can act as a mock for creating a run with [this Data Factory API](https://learn.microsoft.com/en-us/rest/api/datafactory/pipelines/create-run?tabs=HTTP#pipelines_createrun)and getting the status of the run [with this one](https://learn.microsoft.com/en-us/rest/api/datafactory/pipeline-runs/get?tabs=HTTP#pipelineruns_get) correlated by the runId.
  

