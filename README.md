# KPI.Collector
The goal of this project is to gather KPI (key performance indicator) data from various third party services and organize all of it into a single [InfluxDB](https://www.influxdata.com/) bucket so metrics from different sources can be compared and correlated together.

## How it Works
The application works via polling data from third-party data sources on a fixed interval (by default once every 4 hours).

The infrastructure, in production, is hosted as such:

![KPI.Collector Azure Infrastructure](/images/kpi-azure-design.png)

* We use [Azure Functions v3](https://docs.microsoft.com/en-us/azure/azure-functions/functions-versions) to run on a [timer trigger](https://docs.microsoft.com/en-us/azure/azure-functions/functions-bindings-timer), approximately once every 4 hours, to poll data from the various data sources;
* That data is written into an [InfluxDB 2.0 data source](https://www.influxdata.com/) - which can be used to create dashboards either in InfluxDB itself or using an external dashboard tool such as [Grafana](https://grafana.com/);
* We use [Azure Key Vault](https://docs.microsoft.com/en-us/azure/key-vault/general/overview) to store sensitive data, such as API keys for specific services, and that data is securely read via the Azure App Service configuration specific in our [Pulumi](https://www.pulumi.com/) infrastructure and deployment project. These values will be consumed by the Azure Function itself as environment variables.
* We use Azure Pipelines to CI/CD this project into production - each new deployment is triggered by the creation of a new `git tag` in Github'; and
* Finally, we use [Akka.NET](https://getakka.net/) and a number of third-party .NET client libraries to call the APIs that produce the data that is written into InfluxDB. Akka.NET's role is to direct the flow of traffic, know when all of the work required by the system has been completed, and to safely isolate any failed operations so they can't completely fail the entire batch job.

### Supported Data Sources
In terms of data sources we can collect data from, we support:

* Mailchimp
* Google Analytics
* NuGet
* [Sdkbin](https://sdkbin.com/)

**[How to configure your analytics providers](docs/providers.md)**.

### Hosting from Scratch
Here's how to host it:

- Create a resource group on Windows Azure;
- Create an [InfluxDb 2.0 "serverless" data source via Azure Marketplace](https://azuremarketplace.microsoft.com/en-us/marketplace/apps/influxdata.influxdb-cloud?tab=Overview) in that resource group;
- Create a [Pulumi account](https://www.pulumi.com/);
- Clone this repository;
- Override all of the [`Pulumi.dev.yaml` configuration values](src/Petabridge.Collector.Infra/Pulumi.dev.yaml) and secrets with your own settings;
- In the root directory run `build.cmd buildrelease` to compile the solution;
- In the `/src/Petabridge.Collector.Infra` folder, run `pulumi up` to deploy all of the cloud infrastructure and the first version of your app.

Setting up Azure Pipelines for this project can use the `.yaml` files in the [`build-system`](build-system) folder to create the CI pipelines. To create a deployment pipeline, you'll need to manually create a release pipeline.

**TODO**: create video showing how to create release pipeline in Azure Devops.

## Building and Running Locally
To build and run KPI.Collector locally you will need to have the following tools installed on your system:

1. [.NET Core 3.1 SDK](https://dotnet.microsoft.com/download/dotnet/3.1);
2. [Azure Functions Core Tools](https://docs.microsoft.com/en-us/azure/azure-functions/functions-run-local?tabs=windows%2Ccsharp%2Cbash);
3. Docker with Local Kubernetes enabled - for local testing;
4. [Pulumi CLI](https://www.pulumi.com/docs/get-started/install/) - if you wish to deploy to your own cloud environment.

### Compilation
To compile and build KPI.Collector, execute the following commands:

**Windows**
```
PS> ./build.cmd buildrelease
```

**OS X / Linux**
```
$> ./build.sh buildrelease
```

### Local Debugging
To debug and run the Azure Function app locally, first, deploy all of our service mocks for external dependencies (InfluxDb, Azure Storage, etc) via your local Kubernetes cluster:

```
PS> ./k8s/deployAll.cmd
```

This will create a new local K8s namespace, `kpi-collector`, whose output you can view via:

```
PS> kubectl get all -n kpi-collector
```

By default we have the following default services running inside `kpi-collector`:

* InfluXDB 2.0 - view at http://localhost:8086
* Grafana - view at http://localhost:3000
* Azurite Azure Storage Emulator - with blob services hosted at http://localhost:10000

All of these values are configured to be used by default during local development of the `KpiCollector` Azure Function, via [`local.settings.json`](src/Petabridge.KPI.Collector.Functaculous/local.settings.json)

Once these dependencies are deployed, it should be possible to test and debug the Azure Function locally.

#### Running `KpiCollector`
To run the `KpiCollector` Azure Function using the Azure Function Core Tools, switch directories to:

```
PS> cd src/Petabridge.KPI.Collector.Functaculous
```

And then run the following command:

```
PS> func host start
```

This should allow you to preview the function locally on your machine without having to deploy.

## Support

Copyright 2015-2021 Petabridge, LLC