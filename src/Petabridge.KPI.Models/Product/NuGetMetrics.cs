// -----------------------------------------------------------------------
// <copyright file="NuGetMetrics.cs" company="Petabridge, LLC">
//      Copyright (C) 2015 - 2021 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using InfluxDB.Client;
using InfluxDB.Client.Api.Domain;
using InfluxDB.Client.Writes;
using Microsoft.Extensions.Logging;

namespace Petabridge.KPI.Models.Product
{
    public interface INugetMetricsService
    {
        /// <summary>
        /// For a given publisher on NuGet, we shall:
        ///
        /// 1. Query all of the packages they've published
        /// 2. For each package, we shall gather: total installs, installs per day
        /// </summary>
        /// <param name="cancel"></param>
        /// <returns></returns>
        Task<ApiQueryResult> FetchAccountMetrics(CancellationToken cancel);
    }

    public sealed class NuGetConfig
    {
        public NuGetConfig(string[] accountNames)
        {
            AccountNames = accountNames;
        }

        /// <summary>
        /// The set of NuGet accounts we will query data for
        /// </summary>
        public string[] AccountNames { get; }
    }

    /// <summary>
    /// Used to fetch relevant NuGet metrics
    /// </summary>
    public sealed class NugetMetricsService : INugetMetricsService
    {
        private readonly InfluxDBClient _client;
        private readonly ILogger<NugetMetricsService> _logger;
        private readonly NuGetConfig _config;

        public NugetMetricsService(InfluxDBClient client, NuGetConfig config, ILogger<NugetMetricsService> logger)
        {
            _client = client;
            _config = config;
            _logger = logger;
        }


        /// <summary>
        /// Replace me
        /// </summary>
        public class MyFakePackageClass
        {
            public string PackageId { get; set; }
            public string PackageName { get; set; } // what we really care about
            public string CurrentVersion { get; set; }
            public long TotalDownloads { get; set; }
            public long DownloadsPerDay { get; set; }
            public DateTimeOffset LastUpdated { get; set; }
        }

        public async Task<ApiQueryResult> FetchAccountMetrics(CancellationToken cancel)
        {
            try
            {
                foreach (var publisher in _config.AccountNames)
                {
                    // QUERY ALL PACKAGES THAT BELONG TO ACCOUNT

                    var packages = new List<MyFakePackageClass>(); // this gets produced via some API call
                    var dataPoints = new List<PointData>();

                    // do a fast iteration on the top-level package data
                    foreach (var package in packages)
                    {
                        // record stats
                        var builder = PointData.Measurement("nuget_package_stats")
                            .Timestamp(DateTime.UtcNow, WritePrecision.S)
                            .Tag("package", package.PackageName)
                            .Tag("author", publisher)
                            .Tag("service", "nuget")
                            .Field("total_downloads", package.TotalDownloads)
                            .Field("downloads_per_day", package.DownloadsPerDay);

                        dataPoints.Add(builder);

                        // record stats for every package version pushed by publisher
                        // (you can set a limit on it if you really want to)
                    }

                    await _client.GetWriteApiAsync().WritePointsAsync(dataPoints, cancel);
                    _logger.LogInformation("Successfully wrote [{0}] data points for [{1}]", dataPoints.Count,
                        publisher);
                }

                return ApiQueryResult.WriteSuccess.With(nameof(FetchAccountMetrics));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "failed to complete {0}", nameof(FetchAccountMetrics));
                return ApiQueryResult.WriteFailure.With(nameof(FetchAccountMetrics), message: ex.Message);
            }
        }
    }
}