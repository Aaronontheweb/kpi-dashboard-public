﻿// -----------------------------------------------------------------------
// <copyright file="EmailMarketingActor.cs" company="Petabridge, LLC">
//      Copyright (C) 2015 - 2021 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Event;
using Petabridge.KPI.Models.Marketing;

namespace Petabridge.KPI.Collector.Actors.Marketing
{
    public class EmailMarketingActor : UntypedActor
    {
        private readonly IMailingStatsService _statsService;
        private CancellationTokenSource _cts;
        private readonly ILoggingAdapter _log = Context.GetLogger();

        public EmailMarketingActor(IMailingStatsService statsService)
        {
            _statsService = statsService;
        }

        protected override void OnReceive(object message)
        {
            switch (message)
            {
                case StatsProtocol.ScrapeData _: // kick off all email marketing tasks
                    var tasks = new List<Task<ApiQueryResult>>();
                    tasks.Add(_statsService.GetAutomationPerformanceStats(_cts.Token));
                    tasks.Add(_statsService.GetSubscriberStats(_cts.Token));
                    tasks.Add(_statsService.GetTotalCampaignStats(_cts.Token));
                    tasks.Add(_statsService.GetCampaignPerformanceStats(_cts.Token));
                    Task.WhenAll(tasks).PipeTo(Self);
                    break;
                case ApiQueryResult[] queryResults:
                {
                    if (queryResults.All(x => x.StatusCode == ApiQueryResult.WriteSuccess.StatusCode))
                        _log.Info("All EmailMarketing stats recorded successfully.");
                    else
                        _log.Warning("Some EmailMarketing stats recorded with errors.");

                    foreach (var q in queryResults)
                        if (q.StatusCode != ApiQueryResult.WriteSuccess.StatusCode)
                            _log.Warning($"FAILED: {q.ToString()}");
                        else
                            _log.Info($"SUCCESS: {q.ToString()}");

                    Context.Stop(Self); // we're done
                    break;
                }
                case Status.Failure failure:
                {
                    _log.Error(failure.Cause, "re-throwing failure and restarting");
                    throw failure.Cause;
                }
                default:
                {
                    Unhandled(message);
                    break;
                }
            }
        }

        protected override void PreStart()
        {
            _cts = new CancellationTokenSource();
            Self.Tell(StatsProtocol.ScrapeData.Instance);
        }

        protected override void PostStop()
        {
            _cts.Cancel();
        }
    }
}