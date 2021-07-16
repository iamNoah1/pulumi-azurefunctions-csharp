# Pulumi AzureFunctions CSharp
This repo contains a walkthrough for getting started with [Pulumi](https://www.pulumi.com) on Azure, deploying an AzureFunction utilizing CSharp.

Pulumi is a Infrastructure As Code Framework, that allows to build, deploy and manage modern cloud applications and infrastructure using familiar languages.  

## Prerequisites
* [Install Pulumi] (https://www.pulumi.com/docs/get-started/install/)
* [Signup to Pulumi] (https://app.pulumi.com/signup)
* Install C# Runtime (.NET Core 3.1 SDK or later)
* Install [Azure CLI](https://docs.microsoft.com/de-de/cli/azure/install-azure-cli) and login using `az login`

## Create new Pulumi project
* `pulumi login`
* `pulumi new azure-csharp` in an empty directory
* Follow the prompted steps


## Deploy stack
* `dotnet publish function`
* `pulumi up`

## Destroy stack
* `pulumi destroy`