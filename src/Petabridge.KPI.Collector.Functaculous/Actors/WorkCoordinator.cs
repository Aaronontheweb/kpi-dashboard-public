// -----------------------------------------------------------------------
// <copyright file="WorkCoordinator.cs" company="Petabridge, LLC">
//      Copyright (C) 2015 - 2021 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.Generic;
using Akka.Actor;

namespace Petabridge.KPI.Collector.Actors
{
    /// <summary>
    /// Responsible for making sure that all jobs have been completed.
    ///
    /// Once all work is finished the process will terminate itself gracefully.
    /// </summary>
    public sealed class WorkCoordinator : UntypedActor
    {
        private readonly HashSet<IActorRef> _actorsToSupervise;

        public WorkCoordinator(IEnumerable<IActorRef> actorsToSupervise)
        {
            _actorsToSupervise = new HashSet<IActorRef>(actorsToSupervise);
        }

        protected override void OnReceive(object message)
        {
            switch (message)
            {
                case Terminated t:
                {
                    _actorsToSupervise.Remove(t.ActorRef);
                    if (_actorsToSupervise.Count == 0) // all children have completed their work
                        Context.System.Terminate(); // shut the entire ActorSystem

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
            foreach (var actor in _actorsToSupervise) Context.Watch(actor);
        }
    }
}