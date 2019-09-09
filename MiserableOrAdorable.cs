using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using MiserableOrAdorable.Dto;

namespace MiserableOrAdorab
{
    public static class MiserableOrAdorableFunctions
    {
        [FunctionName(nameof(NewEmployee))]
        public static async Task<IActionResult> NewEmployee(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get")] NewEmployeeArgs args,
            [OrchestrationClient] DurableOrchestrationClient durableOrchestrationClient,
            ILogger log)
        {
            var orchestrationId = await durableOrchestrationClient.StartNewAsync(nameof(Orchestration), args);

            log.LogInformation("Started orchestration with ID = '{orchestrationId}'.", orchestrationId);

            var response = durableOrchestrationClient.CreateHttpManagementPayload(orchestrationId);
            
            return new OkObjectResult(response);
        }

        [FunctionName(nameof(Orchestration))]
        public static async Task Orchestration(
            [OrchestrationTrigger] DurableOrchestrationContext context,
            ILogger log)
        {
            var args = context.GetInput<NewEmployeeArgs>();

            log.LogInformation("Starting orchestrator {instanceId}", context.InstanceId); 

            var employeeId = await context.CallActivityAsync<Guid>(nameof(CreateInDb), args);

            log.LogInformation("Employee {id} created in DB", employeeId); 
        }


        [FunctionName(nameof(CreateInDb))]
        public static async Task<Guid> CreateInDb(
            [ActivityTrigger] NewEmployeeArgs newEmployeeArgs,
            ILogger log)
        {
            var employee = new Employee(Guid.NewGuid(), newEmployeeArgs.FullName, newEmployeeArgs.Age);

            if (employee.Age > 100) throw new Exception("I cannot believe this!");

            log.LogInformation("Creating {name} in database", employee.FullName);

            await Task.Delay(TimeSpan.FromSeconds(employee.Age));

            log.LogInformation("{name} created in database", employee.FullName);

            return employee.Id;
        }
    }
}