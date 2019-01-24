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
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using DotNedSaturday.Dto;

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
        public static async Task<string> Orchestration(
            [OrchestrationTrigger] DurableOrchestrationContext context,
            ILogger log)
        {
            var args = context.GetInput<NewEmployeeArgs>();

            log.LogInformation("Hi, Im your orchestrator");

            /*
                    Simple activity
             */
            var employeeId = await context.CallActivityAsync<Guid>(nameof(CreateInDb), args);

            log.LogInformation("Employee {id} created in DB", employeeId);

            /*
                    Chaining
             */
            await context.CallActivityAsync(nameof(SendWelcomeMail), employeeId);

            /*
                    Fan out / Fan in
             */
            var dealersToGetQuote = new string[] { "Athlon", "AA Lease", "Leaseplan" };
            var tasks = new List<Task<LeaseCarGetQuoteResult>>();
            foreach (var dealer in dealersToGetQuote)
            { 
                var task = context.CallActivityAsync<LeaseCarGetQuoteResult>(nameof(LeaseCarGetQuote), new LeaseCarGetQuoteArgs(employeeId, dealer));
                tasks.Add(task);
            } 
            await Task.WhenAll(tasks); 
            var bestQuote = tasks.OrderBy(r => r.Result.Quote).First().Result;

            log.LogInformation("Best quote is {amount} for dealer {dealer}", bestQuote.Quote, bestQuote.DealerName);

            /*
                    Chaining
             */
            context.SetCustomStatus("WaitiForManagerApproval");
            var approved = await context.WaitForExternalEvent<bool>("WaitForManagerApproval", TimeSpan.FromMinutes(1), false);
            if (approved)
            {
                context.SetCustomStatus("Finished");
                return $"End good all good, successfully created employee with id {employeeId}!";
            }

            throw new Exception("Wait wut?");
        }

        public static async Task Run(DurableOrchestrationContext context)
        {
            var expiryTime = new DateTime(2019, 1, 27);

            while (context.CurrentUtcDateTime < expiryTime)
            {
                var nextCheck = context.CurrentUtcDateTime.AddSeconds(30);
                await context.CreateTimer(nextCheck, CancellationToken.None);
            } 
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

        [FunctionName(nameof(SendWelcomeMail))]
        public static async Task SendWelcomeMail(
            [ActivityTrigger] Guid employeeId,
            ILogger log)
        {
            log.LogInformation("Sending welcome mail to employee {employeeId}", employeeId);

            await Task.Delay(TimeSpan.FromSeconds(1));
        }

        [FunctionName(nameof(LeaseCarGetQuote))]
        public static async Task<LeaseCarGetQuoteResult> LeaseCarGetQuote(
            [ActivityTrigger] LeaseCarGetQuoteArgs leaseCarGetQuoteArgs,
            ILogger log)
        {
            log.LogInformation("Getting quote for a lease car for employee {employeeId} and broker {broker}", leaseCarGetQuoteArgs.EmployeeId, leaseCarGetQuoteArgs.DealerName);

            await Task.Delay(TimeSpan.FromSeconds(1));
            var quote = new Random().NextDouble() * 100000;
            
            return new LeaseCarGetQuoteResult(leaseCarGetQuoteArgs.DealerName, quote);
        } 
    }
}