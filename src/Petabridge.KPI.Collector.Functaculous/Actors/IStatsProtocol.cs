// -----------------------------------------------------------------------
// <copyright file="IStatsProtocol.cs" company="Petabridge, LLC">
//      Copyright (C) 2015 - 2021 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Petabridge.KPI.Collector.Actors
{
    public interface IStatsProtocol
    {
    }

    public static class StatsProtocol
    {
        /// <summary>
        /// Query all of our relevant sources' data and then terminate.
        /// </summary>
        public sealed class ScrapeData : IStatsProtocol
        {
            public static readonly ScrapeData Instance = new ScrapeData();

            private ScrapeData()
            {
            }
        }

        public sealed class ScrapeComplete : IStatsProtocol
        {
            public static readonly ScrapeComplete Instance = new ScrapeComplete();

            private ScrapeComplete()
            {
            }
        }
    }
}