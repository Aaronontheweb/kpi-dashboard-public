// -----------------------------------------------------------------------
// <copyright file="Startup.cs" company="Petabridge, LLC">
//      Copyright (C) 2015 - 2021 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Petabridge.KPI.Collector.Services;
using Petabridge.KPI.Models.Marketing;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;

namespace Petabridge.KPI.Collector.Functaculous
{
    public class Startup : FunctionsStartup
    {
        public override void ConfigureAppConfiguration(IFunctionsConfigurationBuilder builder)
        {
            builder.ConfigurationBuilder.AddEnvironmentVariables();
            base.ConfigureAppConfiguration(builder);
        }

        public override void Configure(IFunctionsHostBuilder builder)
        {
            // configure influx first
            builder.Services.AddInfluxDb();

            // configure email services
            builder.Services.AddEmail();

            // configure web analytics
            builder.Services.AddWebAnalytics();

            // Add the Akka.NET service
            builder.Services.AddSingleton<AkkaService>();
        }
    }
}