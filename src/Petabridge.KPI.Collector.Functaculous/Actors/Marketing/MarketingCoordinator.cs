// -----------------------------------------------------------------------
// <copyright file="MarketingCoordinator.cs" company="Petabridge, LLC">
//      Copyright (C) 2015 - 2021 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Akka.Actor;
using Akka.DependencyInjection;

namespace Petabridge.KPI.Collector.Actors.Marketing
{
    public class MarketingCoordinator : UntypedActor
    {
        private readonly HashSet<IActorRef> _children = new HashSet<IActorRef>();

        protected override void OnReceive(object message)
        {
            switch (message)
            {
                case Terminated t:
                {
                    _children.Remove(t.ActorRef);
                    if (_children.Count == 0) // all children have completed their work
                        Context.Stop(Self); // shut ourselves down

                    break;
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
            var sp = ServiceProvider.For(Context.System);
            var emailMarketing = Context.ActorOf(sp.Props<EmailMarketingActor>(), "email-marketing");
            _children.Add(emailMarketing);
            Context.Watch(emailMarketing);

            var webAnalytics = Context.ActorOf(sp.Props<WebAnalyticsActor>(), "web-analytics");
            _children.Add(webAnalytics);
            Context.Watch(webAnalytics);
        }
    }
}