// -----------------------------------------------------------------------
// <copyright file="KpiCollection.cs" company="Petabridge, LLC">
//      Copyright (C) 2015 - 2021 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Petabridge.KPI.Collector.Services;

namespace Petabridge.KPI.Collector.Functaculous
{
    public class KpiCollection
    {
        private readonly AkkaService _akkaService;

        public KpiCollection(AkkaService akkaService)
        {
            _akkaService = akkaService;
        }

        [FunctionName("KpiCollection")]
        public async Task Run([TimerTrigger("0 */5 * * * *")] TimerInfo myTimer, ILogger log)
        {
            log.LogInformation($"C# Timer trigger function executed at: {DateTime.UtcNow}");

            await _akkaService.RunAsync();
        }
    }
}