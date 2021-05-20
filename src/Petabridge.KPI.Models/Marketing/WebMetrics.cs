// -----------------------------------------------------------------------
// <copyright file="WebMetrics.cs" company="Petabridge, LLC">
//      Copyright (C) 2015 - 2021 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Google.Apis.AnalyticsReporting.v4;
using Google.Apis.AnalyticsReporting.v4.Data;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using InfluxDB.Client;
using InfluxDB.Client.Api.Domain;
using InfluxDB.Client.Writes;
using MailChimp.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Petabridge.KPI.Models.Marketing
{
    public static class WebMetricsExtensions
    {
        public static IServiceCollection AddWebAnalytics(this IServiceCollection services)
        {
            services.AddSingleton<AnalyticsReportingService>(sp =>
            {
                var appConfig = sp.GetRequiredService<IConfiguration>();

                var gaServiceAccount = appConfig["GoogleAnalytics:ServiceAccount"];
                var gaServiceKey = appConfig["GoogleAnalytics:Key"];

                // create the GA auth client

                var scopes = new[] {AnalyticsReportingService.Scope.Analytics};
                var init = new ServiceAccountCredential.Initializer(gaServiceAccount) {Scopes = scopes};
                var credentials = new ServiceAccountCredential(init.FromPrivateKey(gaServiceKey));

                var ars = new AnalyticsReportingService(new BaseClientService.Initializer()
                {
                    HttpClientInitializer = credentials,
                    ApplicationName = "KPI.Collector"
                });

                return ars;
            });

            services.AddSingleton<GoogleAnalyticsStatsConfig>(sp =>
            {
                var appConfig = sp.GetRequiredService<IConfiguration>();
                var tuples = appConfig.GetSection("GoogleAnalytics:Sites").GetChildren()
                    .Select(x => (x["ViewId"], x["Domain"])).ToArray();

                return new GoogleAnalyticsStatsConfig(tuples);
            });

            services.AddSingleton<IWebStatsService, GoogleAnalyticsWebStatsService>();

            return services;
        }
    }

    public interface IWebStatsService
    {
        /// <summary>
        /// Queries all of the daily metrics going back to <see cref="daysBack"/> for a
        /// specific hostname in Google Analytics properties.
        /// </summary>
        /// <param name="cancel">A cancellation token.</param>
        /// <param name="daysBack">The number of days back in time to query data about for this service.</param>
        /// <returns>an <see cref="ApiQueryResult"/></returns>
        Task<ApiQueryResult> GetHostMetrics(CancellationToken cancel, int daysBack = 1);
    }

    public sealed class GoogleAnalyticsStatsConfig
    {
        public GoogleAnalyticsStatsConfig((string viewId, string domain)[] views)
        {
            Views = views;
        }

        /// <summary>
        /// Contains a set of "viewId"s
        /// </summary>
        /// <remarks>
        /// See https://stackoverflow.com/questions/36898103/what-is-a-viewid-in-google-analytics for how to
        /// actually select a ViewId for each of the properties you wish to query.
        /// </remarks>
        public (string viewId, string domain)[] Views { get; }
    }

    public sealed class GoogleAnalyticsWebStatsService : IWebStatsService
    {
        private readonly InfluxDBClient _client;
        private readonly ILogger<GoogleAnalyticsWebStatsService> _logger;
        private readonly AnalyticsReportingService _reportingService;
        private readonly GoogleAnalyticsStatsConfig _config;

        public GoogleAnalyticsWebStatsService(AnalyticsReportingService reportingService,
            GoogleAnalyticsStatsConfig config,
            InfluxDBClient client, ILogger<GoogleAnalyticsWebStatsService> logger)
        {
            _reportingService = reportingService;
            _config = config;
            _client = client;
            _logger = logger;
        }

        public async Task<ApiQueryResult> GetHostMetrics(CancellationToken cancel, int daysBack = 1)
        {
            try
            {
                /*
                * See https://developers.google.com/analytics/devguides/reporting/core/dimsmets for details
                * on how to customize this sort of reporting.
                */
                var dateRanges = new List<DateRange>();
                dateRanges.Add(new DateRange {StartDate = $"{daysBack}DaysAgo", EndDate = "today"});

                _logger.LogInformation("Preparing Google Analytics v4 Reporting query for properties [{0}]",
                    string.Join(",", _config.Views));

                // can only do a maximum of 10 metrics per query
                var metrics = new List<Metric>();

                metrics.Add(new Metric() {Expression = "ga:sessions", Alias = "sessions"});
                metrics.Add(new Metric() {Expression = "ga:users", Alias = "users"});
                metrics.Add(new Metric() {Expression = "ga:newUsers", Alias = "unique_users"});
                metrics.Add(new Metric() {Expression = "ga:sessionsPerUser", Alias = "sessions_per_user"});
                metrics.Add(new Metric() {Expression = "ga:sessionDuration", Alias = "session_duration"});
                metrics.Add(new Metric() {Expression = "ga:avgSessionDuration", Alias = "avg_session_duration"});
                //metrics.Add(new Metric() { Expression = "ga:bounces", Alias = "bounces" });
                //metrics.Add(new Metric() { Expression = "ga:bounceRate", Alias = "bounce_rate" });
                metrics.Add(new Metric() {Expression = "ga:pageviews", Alias = "views"});
                metrics.Add(new Metric() {Expression = "ga:pageviewsPerSession", Alias = "views_per_session"});
                metrics.Add(new Metric() {Expression = "ga:uniquePageviews", Alias = "unique_views"});
                metrics.Add(new Metric() {Expression = "ga:timeOnPage", Alias = "time_on_page"});

                // we're just going to look at new vs. returning visitors
                var dimensions = new List<Dimension>();
                dimensions.Add(new Dimension() {Name = "ga:userType"});

                var orderBys = new List<OrderBy>();
                orderBys.Add(new OrderBy() {FieldName = "ga:pageviews", SortOrder = "DESCENDING", OrderType = "VALUE"});

                foreach (var view in _config.Views)
                {
                    var reportRequests = new List<ReportRequest>();

                    var request = new ReportRequest()
                    {
                        DateRanges = dateRanges,
                        IncludeEmptyRows = true,
                        Dimensions = dimensions,
                        Metrics = metrics,
                        OrderBys = orderBys,
                        ViewId = view.viewId
                    };
                    reportRequests.Add(request);


                    var fullQuery = new GetReportsRequest();
                    fullQuery.ReportRequests = reportRequests;

                    var response = await _reportingService.Reports.BatchGet(fullQuery).ExecuteAsync(cancel);

                    var pointData = new List<PointData>();
                    foreach (var report in response.Reports) pointData.AddRange(WriteReport(report, view.domain));

                    await _client.GetWriteApiAsync().WritePointsAsync(pointData, cancel);
                    _logger.LogInformation("Successfully wrote [{0}] data points for [{1}]", pointData.Count,
                        view.domain);

                    if (_logger.IsEnabled(LogLevel
                        .Debug)) // write the line protocol output in detail for debugging purposes
                        foreach (var p in pointData)
                            _logger.LogDebug("Successfully wrote [{0}] for [{1}]", p.ToLineProtocol(), view.domain);
                }

                return ApiQueryResult.WriteSuccess.With(nameof(GetHostMetrics));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "failed to complete {0}", nameof(GetHostMetrics));
                return ApiQueryResult.WriteFailure.With(nameof(GetHostMetrics), message: ex.Message);
            }
        }

        private IEnumerable<PointData> WriteReport(Report report, string domain)
        {
            var points = new List<PointData>();
            var headers = report.ColumnHeader.MetricHeader.MetricHeaderEntries;
            var dimensions = report.ColumnHeader.Dimensions;
            foreach (var r in report.Data.Rows)
            {
                var i = 0;
                var builder = PointData.Measurement("website_performance")
                    .Timestamp(DateTime.UtcNow, WritePrecision.S)
                    .Tag("website", domain)
                    .Tag("service", "googleanalytics");

                foreach (var d in r.Dimensions)
                {
                    builder = builder.Tag(dimensions[0], d);
                    i++;
                }

                foreach (var m in r.Metrics)
                {
                    var d = 0;
                    foreach (var v in m.Values)
                    {
                        switch (headers[d].Type.ToUpperInvariant())
                        {
                            case "INTEGER":
                                builder = builder.Field(headers[d].Name, int.Parse(v));
                                break;
                            case "FLOAT":
                            case "TIME":
                            case "PERCENT":
                                builder = builder.Field(headers[d].Name, double.Parse(v));
                                break;
                            case "CURRENCY":
                                builder = builder.Field(headers[d].Name, decimal.Parse(v));
                                break;
                            default:
                                _logger.LogWarning(
                                    "Skipped field [{0}] because its data type was [{1}], which is unrecognized.",
                                    headers[d].Name, headers[d].Type);
                                break;
                        }

                        d++;
                    }
                }

                points.Add(builder);
            }

            return points;
        }
    }
}