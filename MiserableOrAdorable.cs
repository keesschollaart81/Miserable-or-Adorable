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
using Newtonsoft.Json;

namespace MiserableOrAdorab
{
    public static class MiserableOrAdorableFunctions
    {
        [FunctionName(nameof(NewEmployee))]
        public static async Task<IActionResult> NewEmployee(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get")] NewEmployeeArgs args,
            [DurableClient] IDurableEntityClient durableEntityClient,
            ILogger log)
        {
            var employeeId = new EntityId(nameof(EmployeeEntity), "666");
            await durableEntityClient.SignalEntityAsync<IEmployee>(employeeId, proxy => proxy.Fire("No reason"));

            return new OkObjectResult("Aaaand he's gone!");
        } 
    }


    [JsonObject(MemberSerialization.OptIn)]
    public class EmployeeEntity : IEmployee
    {
         [JsonProperty]
        public string Id { get; set; }

        public EmployeeEntity(string id)
        {
            Id = id;
        }

        public async Task Fire(string reason)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(1));
        }

        [FunctionName(nameof(EmployeeEntity))]
        public static async Task HandleEntityOperation([EntityTrigger] IDurableEntityContext ctx)
        {
            var employeeId = ctx.EntityId;
            await ctx.DispatchAsync<EmployeeEntity>(employeeId.EntityKey);
        }
    }

    public interface IEmployee
    {
        Task Fire(string reason);
    }
}