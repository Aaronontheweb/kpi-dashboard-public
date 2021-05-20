// -----------------------------------------------------------------------
// <copyright file="AkkaService.cs" company="Petabridge, LLC">
//      Copyright (C) 2015 - 2021 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Configuration;
using Akka.DependencyInjection;
using InfluxDB.Client.Api.Domain;
using Microsoft.Extensions.Hosting;
using Petabridge.KPI.Collector.Actors;
using Petabridge.KPI.Collector.Actors.Marketing;
using File = System.IO.File;

namespace Petabridge.KPI.Collector.Services
{
    public class AkkaService
    {
        private readonly IServiceProvider _serviceProvider;
        private IActorRef _workCoordinator;
        private ActorSystem _actorSystem;

        public AkkaService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public async Task RunAsync()
        {
            var config = ConfigurationFactory.ParseString(@"
                    akka.actor.default-dispatcher = {
                        executor = channel-executor
                        fork-join-executor { #channelexecutor will re-use these settings
                        parallelism-min = 2
                        parallelism-factor = 1
                        parallelism-max = 64
                        }
                    }

                    akka.actor.internal-dispatcher = {
                        executor = channel-executor
                        throughput = 5
                        fork-join-executor {
                        parallelism-min = 4
                        parallelism-factor = 1.0
                        parallelism-max = 64
                        }
                    }

                    akka.remote.default-remote-dispatcher {
                        type = Dispatcher
                        executor = channel-executor
                        fork-join-executor {
                        parallelism-min = 2
                        parallelism-factor = 0.5
                        parallelism-max = 16
                        }
                    }

                    akka.remote.backoff-remote-dispatcher {
                    executor = channel-executor
                    fork-join-executor {
                        parallelism-min = 2
                        parallelism-max = 2
                    }
                    }
                ");
            var bootstrap = BootstrapSetup.Create().WithConfig(config);
            var serviceProviderSetup = ServiceProviderSetup.Create(_serviceProvider);
            var actorSystemSetup = bootstrap.And(serviceProviderSetup);

            _actorSystem = ActorSystem.Create("StatsSys", actorSystemSetup);
            var marketingCoordinator =
                _actorSystem.ActorOf(Props.Create(() => new MarketingCoordinator()), "marketing");
            var actors = new[] {marketingCoordinator};

            // start the work coordinator
            _workCoordinator = _actorSystem.ActorOf(Props.Create(() => new WorkCoordinator(actors)), "work");

            await _actorSystem.WhenTerminated.ConfigureAwait(false);
        }
    }
}