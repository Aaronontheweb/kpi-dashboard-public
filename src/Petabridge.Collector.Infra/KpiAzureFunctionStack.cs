// -----------------------------------------------------------------------
// <copyright file="KpiAzureFunctionStack.cs" company="Petabridge, LLC">
//      Copyright (C) 2015 - 2021 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Pulumi;
using Pulumi.Azure.KeyVault;
using Pulumi.AzureNative.Insights;
using Pulumi.AzureNative.KeyVault.Inputs;
using Pulumi.AzureNative.Resources;
using Pulumi.AzureNative.Storage;
using Pulumi.AzureNative.Storage.Inputs;
using Pulumi.AzureNative.Web.Inputs;

internal class KpiAzureFunctionStack : Stack
{
    public static string FunctionAppPublishFolder => Path.Combine("../", "Petabridge.KPI.Collector.Functaculous", "bin",
        "Release", "netcoreapp3.1");

    public KpiAzureFunctionStack()
    {
        var config = new Config();

        var resourceGroupName = config.Require("resourceGroupName");
        var appServicePlan = new Pulumi.AzureNative.Web.AppServicePlan("kpi-linux-asp",
            new Pulumi.AzureNative.Web.AppServicePlanArgs
            {
                Kind = "functionapp",
                ResourceGroupName = resourceGroupName,
                Sku = new SkuDescriptionArgs
                {
                    Name = "Y1",
                    Tier = "Dynamic"
                },
                Tags =
                {
                    {"environment", "dev"},
                    {"product", "kpi-system"}
                }
            });

        // var storageAccount = new Pulumi.AzureNative.Storage.StorageAccount("kpistrg", new Pulumi.AzureNative.Storage.StorageAccountArgs
        // {
        //     Kind = Pulumi.AzureNative.Storage.Kind.StorageV2,
        //     ResourceGroupName = resourceGroupName,
        //     Sku = new Pulumi.AzureNative.Storage.Inputs.SkuArgs
        //     {
        //         Name = "Standard_LRS",
        //     },
        //     Tags = 
        //     {
        //         { "environment", "dev" },
        //         { "product", "kpi-system" },
        //     },
        // });

        var storageAccount = new Pulumi.Azure.Storage.Account("kpistrg", new Pulumi.Azure.Storage.AccountArgs
        {
            ResourceGroupName = resourceGroupName,
            AccountReplicationType = "LRS",
            AccountTier = "Standard",
            AccountKind = "StorageV2"
        });

        var container = new BlobContainer("zips-container", new BlobContainerArgs
        {
            AccountName = storageAccount.Name,
            PublicAccess = PublicAccess.None,
            ResourceGroupName = resourceGroupName
        });

        var blob = new Blob("myfunctions", new BlobArgs
        {
            AccountName = storageAccount.Name,
            ContainerName = container.Name,
            ResourceGroupName = resourceGroupName,
            Source = new FileArchive(FunctionAppPublishFolder),
            Type = BlobType.Block
        });

        var codeBlobUrl = SignedBlobReadUrl(blob, container, storageAccount.Name, resourceGroupName);

        // Application insights
        var appInsights = new Component("appInsights", new ComponentArgs
        {
            ApplicationType = ApplicationType.Web,
            Kind = "web",
            ResourceGroupName = resourceGroupName
        });

        var userAssignedIdentity = new Pulumi.AzureNative.ManagedIdentity.UserAssignedIdentity("kpi-identity",
            new Pulumi.AzureNative.ManagedIdentity.UserAssignedIdentityArgs
            {
                ResourceGroupName = resourceGroupName
            });

        var vault = CreateVault(resourceGroupName, config, userAssignedIdentity);

        /*
         * Create relevant Vault Secrets.
         *
         * N.B.: we use these to make the secrets unreadable for users who have read-access
         * to the Azure Function itself, but not the vault. 
         */
        var influxDbTokenSecret = CreateSecret("InfluxDbToken", config.RequireSecret("InfluxDb.Token"), vault,
            resourceGroupName);
        var mailChimpTokenSecret = CreateSecret("MailchimpApiKey", config.RequireSecret("Mailchimp.ApiKey"), vault,
            resourceGroupName);
        var googleAnalyticsKeySecret = CreateSecret("GoogleAnalyticsKey", config.RequireSecret("GoogleAnalytics.Key"),
            vault, resourceGroupName);

        var kpiCollector = new Pulumi.AzureNative.Web.WebApp("kpi-collector", new Pulumi.AzureNative.Web.WebAppArgs
        {
            Kind = "functionapp,linux",
            ResourceGroupName = resourceGroupName,
            ServerFarmId = appServicePlan.Id,
            SiteConfig = new SiteConfigArgs
            {
                AppSettings =
                    GenerateGoogleAnalyticsArgs(config, "GoogleAnalytics.Sites")
                        .Concat(
                            new[]
                            {
                                new NameValuePairArgs
                                {
                                    Name = "AzureWebJobsStorage",
                                    Value = storageAccount.PrimaryBlobConnectionString
                                },
                                new NameValuePairArgs
                                {
                                    Name = "FUNCTIONS_WORKER_RUNTIME",
                                    Value = "dotnet"
                                },
                                new NameValuePairArgs
                                {
                                    Name = "WEBSITE_RUN_FROM_PACKAGE",
                                    Value = codeBlobUrl
                                },
                                new NameValuePairArgs()
                                {
                                    Name = "FUNCTIONS_EXTENSION_VERSION",
                                    Value = "~3" // use Azure functions v3
                                },

                                /* App Insights - for some reason Azure Functions
                                 * built via Portal use both configuration values
                                 * */

                                new NameValuePairArgs
                                {
                                    Name = "APPLICATIONINSIGHTS_CONNECTION_STRING",
                                    Value = Output.Format($"InstrumentationKey={appInsights.InstrumentationKey}")
                                },
                                new NameValuePairArgs
                                {
                                    Name = "APPINSIGHTS_INSTRUMENTATIONKEY",
                                    Value = appInsights.InstrumentationKey
                                },

                                /* Add InfluxDb parameters
                                 *
                                 * All of these configuration parameters are consumed via
                                 * Microsoft.Extensions.Configuration environment variables
                                 * consumption - hence why we use the double underscore - to separate
                                 * configuration areas.
                                 */
                                new NameValuePairArgs()
                                {
                                    Name = "InfluxDb__ConnectionString",
                                    Value = config.Require("InfluxDb.ConnectionString")
                                },
                                new NameValuePairArgs()
                                {
                                    Name = "InfluxDb__Org",
                                    Value = config.Require("InfluxDb.Org")
                                },
                                new NameValuePairArgs()
                                {
                                    Name = "InfluxDb__Bucket",
                                    Value = config.Require("InfluxDb.Bucket")
                                },
                                new NameValuePairArgs()
                                {
                                    Name = "InfluxDb__Token",
                                    Value = Output.Format(
                                        $"@Microsoft.KeyVault(VaultName={vault.Name};SecretName={influxDbTokenSecret.Name})")
                                },


                                /* Email marketing configuration values */
                                new NameValuePairArgs()
                                {
                                    Name = "Mailchimp__ApiKey",
                                    Value = Output.Format(
                                        $"@Microsoft.KeyVault(VaultName={vault.Name};SecretName={mailChimpTokenSecret.Name})")
                                },

                                /* Web analytics configuration values */
                                new NameValuePairArgs()
                                {
                                    Name = "GoogleAnalytics__ServiceAccount",
                                    Value = config.Require("GoogleAnalytics.ServiceAccount")
                                },

                                new NameValuePairArgs()
                                {
                                    Name = "GoogleAnalytics__Key",
                                    Value = Output.Format(
                                        $"@Microsoft.KeyVault(VaultName={vault.Name};SecretName={googleAnalyticsKeySecret.Name})")
                                }
                            }).ToArray()
            },
            Identity = new ManagedServiceIdentityArgs()
            {
                Type = Pulumi.AzureNative.Web.ManagedServiceIdentityType.SystemAssigned
            },
            Tags =
            {
                {"environment", "dev"},
                {"product", "kpi-system"}
            }
        });

        // need to create access policies using the azure function system identity
        var appId = kpiCollector.Identity.Apply(s => s.PrincipalId);


        var accessPolicy = new AccessPolicy("kpi-keyvault-access",
            new AccessPolicyArgs()
            {
                ObjectId = appId,
                SecretPermissions = "get",
                KeyVaultId = vault.Id,
                TenantId = config.Require("azureTenantId")
            });
    }

    private class SiteValue
    {
        public string ViewId { get; set; }

        public string Domain { get; set; }
    }

    private class GoogleAnalytics
    {
        public List<SiteValue> Sites { get; set; }
    }

    private List<NameValuePairArgs> GenerateGoogleAnalyticsArgs(Config config, string objectName)
    {
        var list = new List<NameValuePairArgs>();
        var sites = config.GetObject<GoogleAnalytics>("GoogleAnalytics");
        var i = 0;
        foreach (var s in sites.Sites)
        {
            list.Add(new NameValuePairArgs()
            {
                Name = $"GoogleAnalytics__Sites__{i}__ViewId",
                Value = s.ViewId
            });

            list.Add(new NameValuePairArgs()
            {
                Name = $"GoogleAnalytics__Sites__{i}__Domain",
                Value = s.Domain
            });

            i++;
        }

        return list;
    }

    private Pulumi.AzureNative.KeyVault.Vault CreateVault(string resourceGroupName, Config config,
        Pulumi.AzureNative.ManagedIdentity.UserAssignedIdentity reader)
    {
        var kpiDevelopmentVault = new Pulumi.AzureNative.KeyVault.Vault("kpivault",
            new Pulumi.AzureNative.KeyVault.VaultArgs
            {
                Properties = new VaultPropertiesArgs
                {
                    AccessPolicies =
                    {
                        CreateAccessPolicy(reader.PrincipalId, config)
                    },
                    Sku = new Pulumi.AzureNative.KeyVault.Inputs.SkuArgs
                    {
                        Family = "A",
                        Name = Pulumi.AzureNative.KeyVault.SkuName.Standard
                    },
                    SoftDeleteRetentionInDays = 90,
                    TenantId = config.Require("azureTenantId")
                },
                ResourceGroupName = resourceGroupName,
                Tags =
                {
                    {"environment", "dev"},
                    {"product", "kpi-system"}
                }
            });

        return kpiDevelopmentVault;
    }

    private AccessPolicyEntryArgs CreateAccessPolicy(Output<string> objectId, Config config)
    {
        return new AccessPolicyEntryArgs
        {
            ObjectId = objectId,
            Permissions = new PermissionsArgs
            {
                Certificates = { },
                Keys =
                {
                    "Get"
                },
                Secrets =
                {
                    "Get"
                }
            },
            TenantId = config.Require("azureTenantId")
        };
    }

    private Pulumi.AzureNative.KeyVault.Secret CreateSecret(string secretName, Output<string> secretValue,
        Pulumi.AzureNative.KeyVault.Vault vault, string resourceGroupName)
    {
        var secret = new Pulumi.AzureNative.KeyVault.Secret(secretName, new Pulumi.AzureNative.KeyVault.SecretArgs
        {
            Properties = new SecretPropertiesArgs
            {
                Value = secretValue
            },
            ResourceGroupName = resourceGroupName,
            SecretName = secretName,
            VaultName = vault.Name
        });

        return secret;
    }

    [Output] public Output<string> Endpoint { get; set; }

    private static Output<string> SignedBlobReadUrl(Blob blob, BlobContainer container, Output<string> account,
        string resourceGroupName)
    {
        return Output.Tuple<string, string, string, string>(
            blob.Name, container.Name, account, resourceGroupName).Apply(t =>
        {
            (string blobName, string containerName, string accountName, string rgName) = t;

            var blobSAS = ListStorageAccountServiceSAS.InvokeAsync(new ListStorageAccountServiceSASArgs
            {
                AccountName = accountName,
                Protocols = HttpProtocol.Https,
                SharedAccessStartTime = "2021-01-01",
                SharedAccessExpiryTime = "2030-01-01",
                Resource = SignedResource.C,
                ResourceGroupName = rgName,
                Permissions = Permissions.R,
                CanonicalizedResource = "/blob/" + accountName + "/" + containerName,
                ContentType = "application/json",
                CacheControl = "max-age=5",
                ContentDisposition = "inline",
                ContentEncoding = "deflate"
            });
            return Output.Format(
                $"https://{accountName}.blob.core.windows.net/{containerName}/{blobName}?{blobSAS.Result.ServiceSasToken}");
        });
    }

    private static Output<string> GetConnectionString(Input<string> resourceGroupName, Input<string> accountName)
    {
        // Retrieve the primary storage account key.
        var storageAccountKeys = Output.All<string>(resourceGroupName, accountName).Apply(t =>
        {
            var resourceGroupName = t[0];
            var accountName = t[1];
            return ListStorageAccountKeys.InvokeAsync(
                new ListStorageAccountKeysArgs
                {
                    ResourceGroupName = resourceGroupName,
                    AccountName = accountName
                });
        });
        return storageAccountKeys.Apply(keys =>
        {
            var primaryStorageKey = keys.Keys[0].Value;

            // Build the connection string to the storage account.
            return Output.Format(
                $"DefaultEndpointsProtocol=https;AccountName={accountName};AccountKey={primaryStorageKey}");
        });
    }
}