// -----------------------------------------------------------------------
// <copyright file="EmailMetrics.cs" company="Petabridge, LLC">
//      Copyright (C) 2015 - 2021 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Threading;
using System.Linq;
using System.Threading.Tasks;
using InfluxDB.Client;
using InfluxDB.Client.Api.Domain;
using InfluxDB.Client.Core;
using InfluxDB.Client.Writes;
using MailChimp.Net.Core;
using MailChimp.Net.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using MailChimp.Net;

namespace Petabridge.KPI.Models.Marketing
{
    public static class MailingServiceExtensions
    {
        public static IServiceCollection AddEmail(this IServiceCollection services)
        {
            services.AddSingleton<IMailChimpManager>(sp =>
            {
                var appConfig = sp.GetRequiredService<IConfiguration>();
                var mailchimpApiKey = appConfig["Mailchimp:ApiKey"];
                return new MailChimpManager(mailchimpApiKey);
            });

            services.AddSingleton<IMailingStatsService, MailchimpStatsService>();

            return services;
        }
    }

    // Exists as an interface because there's going to be more than one implementation
    public interface IMailingStatsService
    {
        Task<ApiQueryResult> GetSubscriberStats(CancellationToken cancel);

        Task<ApiQueryResult> GetTotalCampaignStats(CancellationToken cancel);

        Task<ApiQueryResult> GetAutomationPerformanceStats(CancellationToken cancel);

        Task<ApiQueryResult> GetCampaignPerformanceStats(CancellationToken cancel);
    }

    public sealed class MailchimpStatsService : IMailingStatsService
    {
        private readonly InfluxDBClient _client;
        private readonly ILogger<MailchimpStatsService> _logger;
        private readonly IMailChimpManager _mailchimp;

        public MailchimpStatsService(IMailChimpManager mailchimp, InfluxDBClient client,
            ILogger<MailchimpStatsService> logger)
        {
            _mailchimp = mailchimp;
            _client = client;
            _logger = logger;
        }

        public async Task<ApiQueryResult> GetAutomationPerformanceStats(CancellationToken cancel)
        {
            try
            {
                var automations = await _mailchimp.Automations.GetAllAsync(new QueryableBaseRequest() { });

                if (automations == null || automations.Count() == 0)
                    _logger.LogError("No automations found!");

                if (cancel.IsCancellationRequested)
                    return ApiQueryResult.Cancelled.With(nameof(GetAutomationPerformanceStats));

                var points = new List<PointData>();
                foreach (var r in automations.Where(x => x.Status == MailChimp.Net.Models.AutomationStatus.Sending))
                {
                    var builder = PointData.Measurement("email_automation_performance")
                        .Timestamp(DateTime.UtcNow, WritePrecision.S)
                        .Tag("service", "mailchimp")
                        .Tag("list_name", r.Recipients.ListName)
                        .Tag("campaign_name", r.Settings.Title)
                        .Field("emails_sent", r.EmailsSent)
                        .Field("clicks_total", r.ReportSummary.Clicks)
                        .Field("clicks_rate", r.ReportSummary.ClickRate)
                        .Field("opens_total", r.ReportSummary.Opens)
                        .Field("opens_unique", r.ReportSummary.UniqueOpens)
                        .Field("opens_rate", r.ReportSummary.OpenRate)
                        .Field("clicks_subscribers_unique", r.ReportSummary.SubscriberClicks);

                    points.Add(builder);
                }

                var writeApi = _client.GetWriteApiAsync();
                await writeApi.WritePointsAsync(points, cancel);
                foreach (var p in points) _logger.LogInformation("wrote data for [{0}]", p.ToLineProtocol());

                return ApiQueryResult.WriteSuccess.With(nameof(GetAutomationPerformanceStats));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "failed to complete {0}", nameof(GetAutomationPerformanceStats));
                return ApiQueryResult.WriteFailure.With(nameof(GetAutomationPerformanceStats), message: ex.Message);
            }
        }

        public async Task<ApiQueryResult> GetCampaignPerformanceStats(CancellationToken cancel)
        {
            try
            {
                var reports = await _mailchimp.Reports.GetAllReportsAsync(new ReportRequest()
                    {SinceSendTime = DateTime.UtcNow.AddDays(-15d)});

                if (cancel.IsCancellationRequested)
                    return ApiQueryResult.Cancelled.With(nameof(GetCampaignPerformanceStats));

                var points = new List<PointData>();
                foreach (var r in reports)
                {
                    var builder = PointData.Measurement("email_campaign_performance")
                        .Timestamp(DateTime.UtcNow, WritePrecision.S)
                        .Tag("service", "mailchimp")
                        .Tag("campaign_name", r.CampaignTitle)
                        .Field("opens_total", r.Opens.OpensTotal)
                        .Field("opens_unique", r.Opens.UniqueOpens)
                        .Field("opens_rate", r.Opens.OpenRate)
                        .Field("bounce_hard", r.Bounces.HardBounces)
                        .Field("bounce_soft", r.Bounces.SoftBounces)
                        .Field("clicks_total", r.Clicks.ClicksTotal)
                        .Field("clicks_rate", r.Clicks.ClickRate)
                        .Field("clicks_unique", r.Clicks.UniqueClicks)
                        .Field("clicks_subscribers_unique", r.Clicks.UniqueSubscriberClicks)
                        .Field("unsubscribed", r.Unsubscribed);

                    points.Add(builder);
                }

                var writeApi = _client.GetWriteApiAsync();
                await writeApi.WritePointsAsync(points, cancel);
                foreach (var p in points) _logger.LogInformation("wrote data for [{0}]", p.ToLineProtocol());

                return ApiQueryResult.WriteSuccess.With(nameof(GetCampaignPerformanceStats));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "failed to complete {0}", nameof(GetCampaignPerformanceStats));
                return ApiQueryResult.WriteFailure.With(nameof(GetCampaignPerformanceStats), message: ex.Message);
            }
        }

        public async Task<ApiQueryResult> GetSubscriberStats(CancellationToken cancel)
        {
            try
            {
                var lists = await _mailchimp.Lists.GetAllAsync(new ListRequest() {Limit = 10}).ConfigureAwait(false);

                if (cancel.IsCancellationRequested)
                    return ApiQueryResult.Cancelled.With(nameof(GetSubscriberStats));


                var points = new List<PointData>();
                foreach (var list in lists)
                {
                    var builder = PointData.Measurement("email_subscribers")
                        .Tag("list_name", list.Name)
                        .Tag("service", "mailchimp")
                        .Timestamp(DateTime.UtcNow, WritePrecision.S)
                        .Field("total_subscribers", list.Stats.MemberCount)
                        .Field("total_unsubscribers", list.Stats.UnsubscribeCount)
                        .Field("total_cleaned", list.Stats.CleanedCount);

                    points.Add(builder);
                }

                var writeApi = _client.GetWriteApiAsync();
                await writeApi.WritePointsAsync(points, cancel);
                foreach (var p in points) _logger.LogInformation("wrote data for [{0}]", p.ToLineProtocol());

                return ApiQueryResult.WriteSuccess.With(nameof(GetSubscriberStats));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "failed to complete {0}", nameof(GetSubscriberStats));
                return ApiQueryResult.WriteFailure.With(nameof(GetSubscriberStats), message: ex.Message);
            }
        }

        public async Task<ApiQueryResult> GetTotalCampaignStats(CancellationToken cancel)
        {
            try
            {
                // look at all campaigns over the past 15 days
                var newCampaigns = await _mailchimp.Campaigns.GetAllAsync(new CampaignRequest()
                {
                    SinceSendTime = DateTime.UtcNow.AddDays(-300d),
                    Status = CampaignStatus.Sent | CampaignStatus.Sending
                });

                if (cancel.IsCancellationRequested)
                    return ApiQueryResult.Cancelled.With(nameof(GetTotalCampaignStats));

                var points = new List<PointData>();
                foreach (var c in newCampaigns)
                {
                    var builder = PointData.Measurement("email_campaigns")
                        .Timestamp(c.SendTime?.ToUniversalTime() ?? DateTime.UtcNow, WritePrecision.S)
                        .Tag("service", "mailchimp")
                        .Tag("list_name", c.Recipients.ListName)
                        .Tag("campaign_name", c.Settings.Title)
                        .Field("recipients", c.Recipients.RecipientCount)
                        .Field("campaigns_sent", 1)
                        .Field("emails_sent", c.EmailsSent ?? 0);

                    points.Add(builder);
                }


                var writeApi = _client.GetWriteApiAsync();
                await writeApi.WritePointsAsync(points, cancel);
                foreach (var p in points) _logger.LogInformation("wrote data for [{0}]", p.ToLineProtocol());

                return ApiQueryResult.WriteSuccess.With(nameof(GetTotalCampaignStats));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "failed to complete {0}", nameof(GetTotalCampaignStats));
                return ApiQueryResult.WriteFailure.With(nameof(GetTotalCampaignStats), message: ex.Message);
            }
        }
    }
}