using Pulumi;
using Pulumi.AzureNative.Resources;
using Pulumi.AzureNative.Storage;
using Pulumi.AzureNative.Storage.Inputs;
using Pulumi.AzureNative.Insights;
using Pulumi.AzureNative.Web;
using Pulumi.AzureNative.Web.Inputs;

class FunctionStack : Stack
{
    [Output] public Output<string> Endpoint { get; set; }

    public FunctionStack()
    {
        var resourceGroup = new ResourceGroup("resourceGroup");

        var storageAccount = new StorageAccount("sa", new StorageAccountArgs
        {
            ResourceGroupName = resourceGroup.Name,
            Sku = new SkuArgs
            {
                Name = SkuName.Standard_LRS
            },
            Kind = Pulumi.AzureNative.Storage.Kind.StorageV2
        });

        var appServicePlan = new AppServicePlan("functions-linux-asp", new AppServicePlanArgs
        {
            ResourceGroupName = resourceGroup.Name,
            Kind = "Linux",

            Sku = new SkuDescriptionArgs
            {
                Tier = "Dynamic",
                Name = "Y1"
            },
            // For Linux, you need to change the plan to have Reserved = true property.
            Reserved = true
        });

        var container = new BlobContainer("zips-container", new BlobContainerArgs
        {
            AccountName = storageAccount.Name,
            PublicAccess = PublicAccess.None,
            ResourceGroupName = resourceGroup.Name,
        });

        var blob = new Blob("zip", new BlobArgs
        {
            AccountName = storageAccount.Name,
            ContainerName = container.Name,
            ResourceGroupName = resourceGroup.Name,
            Type = BlobType.Block,
            Source = new FileArchive("../function/bin/Debug/netcoreapp3.1/publish")
        });

        var codeBlobUrl = SignedBlobReadUrl(blob, container, storageAccount, resourceGroup);

        // Application insights
        var appInsights = new Component("appInsights", new ComponentArgs
        {
            ApplicationType = ApplicationType.Web,
            Kind = "web",
            ResourceGroupName = resourceGroup.Name,
        });

        var app = new WebApp("app", new WebAppArgs
        {
            Kind = "FunctionApp",
            ResourceGroupName = resourceGroup.Name,
            ServerFarmId = appServicePlan.Id,
            SiteConfig = new SiteConfigArgs
            {
                AppSettings = new[]
                {
                    new NameValuePairArgs{
                        Name = "AzureWebJobsStorage",
                        Value = GetConnectionString(resourceGroup.Name, storageAccount.Name),
                    },
                    //do we really need this? 
                    new NameValuePairArgs{
                        Name = "runtime",
                        Value = "dotnet",
                    },
                    new NameValuePairArgs{
                        Name = "FUNCTIONS_WORKER_RUNTIME",
                        Value = "dotnet",
                    },
                    new NameValuePairArgs{
                        Name = "WEBSITE_RUN_FROM_PACKAGE",
                        Value = codeBlobUrl,
                    },
                    new NameValuePairArgs{
                        Name = "APPLICATIONINSIGHTS_CONNECTION_STRING",
                        Value = Output.Format($"InstrumentationKey={appInsights.InstrumentationKey}"),
                    },
                },
            },
        });
        this.Endpoint = Output.Format($"https://{app.DefaultHostName}/api/Hello?name=Dude");
    }   

    private static Output<string> SignedBlobReadUrl(Blob blob, BlobContainer container, StorageAccount account, ResourceGroup resourceGroup)
    {
        return Output.Tuple<string, string, string, string>(
            blob.Name, container.Name, account.Name, resourceGroup.Name).Apply(t =>
        {
            (string blobName, string containerName, string accountName, string resourceGroupName) = t;

            var blobSAS = ListStorageAccountServiceSAS.InvokeAsync(new ListStorageAccountServiceSASArgs
            {
                AccountName = accountName,
                Protocols = HttpProtocol.Https,
                SharedAccessStartTime = "2021-01-01",
                SharedAccessExpiryTime = "2030-01-01",
                Resource = SignedResource.C,
                ResourceGroupName = resourceGroupName,
                Permissions = Permissions.R,
                CanonicalizedResource = "/blob/" + accountName + "/" + containerName,
                ContentType = "application/json",
                CacheControl = "max-age=5",
                ContentDisposition = "inline",
                ContentEncoding = "deflate",
            });
            return Output.Format($"https://{accountName}.blob.core.windows.net/{containerName}/{blobName}?{blobSAS.Result.ServiceSasToken}");
        });
    }
    
    private static Output<string> GetConnectionString(Input<string> resourceGroupName, Input<string> accountName)
    {
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
            return Output.Format($"DefaultEndpointsProtocol=https;AccountName={accountName};AccountKey={primaryStorageKey}");
        });
    }
}
