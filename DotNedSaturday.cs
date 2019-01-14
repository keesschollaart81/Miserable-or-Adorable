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

namespace DotNedSaturday
{
    public static class DotNedSaturdayFunctions
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

            log.LogInformation("Hi, Im your orchestrator");

            var employeeId = await context.CallActivityAsync<Guid>(nameof(CreateInDb), args);

            log.LogInformation("Employee {id} created in DB", employeeId);

            var approved = await context.WaitForExternalEvent<bool>("WaitForManagerApproval", TimeSpan.FromSeconds(30), false);

        }

        [FunctionName(nameof(CreateInDb))]
        public static async Task<Guid> CreateInDb(
            [ActivityTrigger] NewEmployeeArgs newEmployeeArgs,
            ILogger log)
        {
            var employee = new Employee(Guid.NewGuid(), newEmployeeArgs.FullName, newEmployeeArgs.Age);

            if (employee.Age > 100) throw new Exception("Daar geloof ik niets van");

            log.LogInformation("Creating {name} in database", employee.FullName);

            await Task.Delay(TimeSpan.FromSeconds(employee.Age));

            log.LogInformation("{name} created in database", employee.FullName);

            return employee.Id;
        }

        [FunctionName(nameof(ApproveEmployee))]
        public static async Task<IActionResult> ApproveEmployee(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get")]  HttpRequest req,
            [OrchestrationClient] DurableOrchestrationClient durableOrchestrationClient,
            ILogger log)
        {
            await durableOrchestrationClient.RaiseEventAsync(req.Query["instanceId"], "WaitForManagerApproval");

            return new OkResult();
        }
    }

    public class NewEmployeeArgs
    {
        public string FullName { get; set; }

        public int Age { get; set; }
    }

    public class Employee
    {
        public Employee(Guid id, string fullName, int age)
        {
            Id = id;
            FullName = fullName;
            Age = age;
        }

        public Guid Id { get; set; }

        public string FullName { get; set; }

        public int Age { get; set; }

        public bool IsApproved { get; set; }
    }
}