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
        public static async Task<object> Orchestration(
            [OrchestrationTrigger] DurableOrchestrationContext context,
            ILogger log)
        {
            var args = context.GetInput<NewEmployeeArgs>();

            log.LogInformation("Starting orchestrator {instanceId}", context.InstanceId);

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
                    Monitoring
             */
            await context.CallActivityAsync(nameof(AskFeedbackActivity), employeeId);

            /*
                    Human Intervention
             */
            context.SetCustomStatus(new { WhatsUp = "WaitingForManagerApproval"});
            var approved = await context.WaitForExternalEvent<bool>("WaitForManagerApproval", TimeSpan.FromMinutes(1), false);
            if (approved)
            {
                context.SetCustomStatus("Finished");
                return new
                {
                    WhatDoYouThink = $"Epic! Successfully created employee with id {employeeId}!"
                };
            }

            throw new Exception("Wait wut?");
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

        [FunctionName(nameof(AskFeedback))]
        public static async Task AskFeedback(
            [OrchestrationTrigger]DurableOrchestrationContext context)
        {
            var dates = new DateTime[]{
                context.CurrentUtcDateTime.AddDays(1),
                context.CurrentUtcDateTime.AddMonths(2),
                context.CurrentUtcDateTime.AddYears(4)
            };

            foreach (var date in dates)
            {
                // todo: send a mail or so

                await context.CreateTimer(date, CancellationToken.None);
            }
        }

        [FunctionName(nameof(AskFeedbackActivity))]
        public static async Task AskFeedbackActivity(
            [ActivityTrigger] Guid employeeId,
            [OrchestrationClient] DurableOrchestrationClient durableOrchestrationClient)
        {
            await durableOrchestrationClient.StartNewAsync(nameof(AskFeedback), null);
        }

        // [FunctionName(nameof(TryGetQuote))]
        // public static async Task<double> TryGetQuote(
        //     [OrchestrationTrigger] DurableOrchestrationContext context)
        // { 
        //     using (var cts = new CancellationTokenSource())
        //     {
        //         var activityTask = context.CallActivityAsync<double>(nameof(LeaseCarGetQuote), null);
        //         var timeoutTask = context.CreateTimer(context.CurrentUtcDateTime.Add(TimeSpan.FromSeconds(30)), cts.Token);

        //         var winner = await Task.WhenAny(activityTask, timeoutTask);
        //         if (winner == activityTask)
        //         {
        //             // success case
        //             cts.Cancel();
        //             return activityTask.Result;
        //         }
        //         else
        //         {
        //             // timeout case
        //             return -1;
        //         }
        //     }
        // }

        // [FunctionName(nameof(SalaryPayout))]
        // public static async Task SalaryPayout(
        //     [OrchestrationTrigger] DurableOrchestrationContext context)
        // {
        //     await context.CallActivityAsync("TransferMoney",null);

        //     var nextPayout = context.CurrentUtcDateTime.AddMonths(1);
        //     await context.CreateTimer(nextPayout, CancellationToken.None);

        //     context.ContinueAsNew(null);
        // }
    }
}