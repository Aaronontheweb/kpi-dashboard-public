# KPI Collection Providers

The KPI.Collector is designed to gather data from various data sources and plot all of it together inside an InfluxDB instance that has indefinite data retention enabled.

But in order to run these providers, some configuration options are required.

### Working with Pulumi
To deploy providers and their configuration to production, we will be using [Pulumi](https://www.pulumi.com/) - a popular infrastructure-as-code product that makes it easy to configure Azure resources (and many others) via self-contained C# / Python / TypeScript / Go applications.

All Pulumi commands need to be called while inside the `/src/Petabridge.Collector.Infra` project, which is where the [Pulumi Stack](https://www.pulumi.com/docs/intro/concepts/stack/) for this project is defined.

## Marketing

### Mailchimp Provider
To enable [Mailchimp Marketing API](https://mailchimp.com/developer/marketing/api/) support, you will need the following:

1. A [Mailchimp API key](https://mailchimp.com/help/about-api-keys/) with read-only access enabled and
2. You will need to that API key as a secret in Pulumi or `dotnet user-secrets` in order to run locally.

#### Pulumi (Live on Azure)

```
ps> cd /src/Petabridge.Collector.Infra
ps> pulumi config set Mailchimp.ApiKey --secret {your key}
```

This will encrypt your key using your configured Pulumi secrets provider. Pulumi will automatically store this API key into a corresponding Azure Key Vault secret upon successful `pulumi up`.

#### `dotnet user-secrets`

```
ps> cd /src/Petabridge.KP.Collector.Functaculous
ps> dotnet user-secrets set Mailchimp:ApiKey {your key}
```

### Google Analytics Provider
To query the [Google Analytics Reporting V4 API](https://developers.google.com/analytics/devguides/reporting/core/v4/), you will need to do the following:

1. Create [a project and corresponding Google Service account](https://support.google.com/a/answer/7378726?hl=en);
2. Download the key created for that service account into a local `.PEM` file;
3. Add that service account email address to all of your Google Analytics properties [via Google Analytics user management](https://support.google.com/analytics/answer/1009702?hl=en&ref_topic=6014099#zippy=%2Cin-this-article);
4. Get the [ViewId for each web property in Google Analytics](https://stackoverflow.com/questions/36898103/what-is-a-viewid-in-google-analytics) you wish to query.

#### Pulumi (Live on Azure)
First, we are going to set the service account email address in Pulumi.

```
ps> cd /src/Petabridge.Collector.Infra
ps> pulumi config set GoogleAnalytics.ServiceAccount {service account}
```

Next, we are going to extract the key (if you downloaded the file as JSON, it should be just the "private key" field and not the entire JSON object - move that into its own file before doing this step) and save that as a secret inside Pulumi.

```
ps> cd /src/Petabridge.Collector.Infra
ps> $myGaKey = $ cat {path-to-key}/{keyfile}
ps> $myGaKey | pulumi config set GoogleAnalytics.Key --secret
```

This will copy the contents of the key, which consists of multiple lines of text, into a single Pulumi secret.

Next, we have to set up all of the different Google Analytics ViewIds and domains we want to query, individiually:

```
ps> cd /src/Petabridge.Collector.Infra
ps> pulumi config set --path 'GoogleAnalytics.Sites[0].Domain' domain1.com
ps> pulumi config set --path 'GoogleAnalytics.Sites[0].ViewId' 123232
ps> pulumi config set --path 'GoogleAnalytics.Sites[1].Domain' domain2.com
ps> pulumi config set --path 'GoogleAnalytics.Sites[1].ViewId' 4545454
```

This will produce a YAML configuration in `Pulumi.dev.yaml` (if your stack is named `dev`) that looks like this:

```
config:
  Petabridge.Collector.Infra:GoogleAnalytics:
    Sites:
    - Domain: domain1.com
      ViewId: 123232
    - Domain: domain2.com
      ViewId: 4545454
```

You can add as many domains as you want - KPI.Collector will automatically index and list all of them.

#### `dotnet user-secrets` and `local.settings.json`

First, we're going to copy our private key into `dotnet user-secrets`.

```
ps> cd /src/Petabridge.Collector.Infra
ps> $myGaKey = $ cat {path-to-key}/{keyfile}
ps> $myGaKey |  dotnet user-secrets set GoogleAnalytics:Key
```

Next, the rest of the values we want to pass into our service for local testing can be mapped into [`local.settings.json`](../src/Petabridge.KPI.Collector.Functaculous):

```json
 "Values": {
    // other vlaues
    "GoogleAnalytics__ServiceAccount": "yourserviceaccount@somegoogledomain.com",
    "GoogleAnalytics__Sites__0__ViewId": "123232",
    "GoogleAnalytics__Sites__0__Domain": "domain1.com",
    "GoogleAnalytics__Sites__1__ViewId": "4545454",
    "GoogleAnalytics__Sites__1__Domain": "domain2.com",
  },
```

These will settings will be mapped onto the KPI.Collector application as environment variables, hence why the names of these types follow the `Microsoft.Extensions.Configuration` environment variable scheme.