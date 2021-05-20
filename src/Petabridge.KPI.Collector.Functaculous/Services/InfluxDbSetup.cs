// -----------------------------------------------------------------------
// <copyright file="InfluxDbSetup.cs" company="Petabridge, LLC">
//      Copyright (C) 2015 - 2021 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using InfluxDB.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Petabridge.KPI.Collector.Services
{
    public static class InfluxDbSetup
    {
        public static IServiceCollection AddInfluxDb(this IServiceCollection services)
        {
            services.AddSingleton<InfluxDBClient>(sp =>
            {
                var configuration = sp.GetRequiredService<IConfiguration>();
                var log = sp.GetRequiredService<ILogger<InfluxDBClient>>();

                InfluxDBClient factory;
                var influxOptions = InfluxDBClientOptions.Builder.CreateNew()
                    .Url(configuration["InfluxDb:ConnectionString"])
                    .Bucket(configuration["InfluxDb:Bucket"])
                    .Org(configuration["InfluxDb:Org"])
                    .AuthenticateToken(configuration["InfluxDb:Token"]).Build();

                // to validate that our live environment variable configuration is being read correctly
                log.LogInformation("Writing to InfluxDb instance [{0}]", configuration["InfluxDb:ConnectionString"]);

                factory = InfluxDBClientFactory.Create(influxOptions);

                return factory;
            });

            return services;
        }
    }
}