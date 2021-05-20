// -----------------------------------------------------------------------
// <copyright file="Program.cs" company="Petabridge, LLC">
//      Copyright (C) 2015 - 2021 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System.Threading.Tasks;
using Pulumi;

internal class Program
{
    private static Task<int> Main()
    {
        return Deployment.RunAsync<KpiAzureFunctionStack>();
    }
}