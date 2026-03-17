// Subscription-scoped entry point for the ACA deployment.
// Mirrors the structure of main.bicep but targets Container Apps instead of App Service.
// The existing main.bicep and resources.bicep are NOT modified.
targetScope = 'subscription'

@minLength(1)
@maxLength(64)
@description('Environment name used to generate a unique hash for resource names.')
param environmentName string

@minLength(1)
@description('Primary location for all resources.')
param location string

@description('Optional — Resource Group name. Defaults to rg-aca-{environmentName}.')
param resourceGroupName string = ''

@description('Optional — Container App name. Defaults to a generated unique name.')
param containerAppName string = ''

@description('Optional — Container Registry name. Defaults to a generated unique name.')
param containerRegistryName string = ''

@description('Optional — Log Analytics Workspace name. Defaults to a generated unique name.')
param logAnalyticsWorkspaceName string = ''

@description('Optional — ACA Managed Environment name. Defaults to a generated unique name.')
param managedEnvironmentName string = ''

@description('SQL Server connection string for the existing database.')
@secure()
param sqlConnectionString string

@description('Azure AD / Entra ID Tenant ID for JWT bearer validation.')
param azureAdTenantId string

@description('Azure AD / Entra ID Client ID (App Registration) for JWT bearer validation.')
param azureAdClientId string

@description('Azure AD / Entra ID authority base URL.')
param azureAdInstance string = 'https://login.microsoftonline.com/'

var resourceToken = toLower(uniqueString(subscription().id, environmentName, location))
var tags = { 'azd-env-name': environmentName }

resource resourceGroup 'Microsoft.Resources/resourceGroups@2022-09-01' = {
  name: !empty(resourceGroupName) ? resourceGroupName : 'rg-aca-${environmentName}'
  location: location
  tags: tags
}

module resources './resources-aca.bicep' = {
  name: 'resources-aca'
  scope: resourceGroup
  params: {
    location: location
    tags: tags
    containerAppName: !empty(containerAppName) ? containerAppName : 'ca-${resourceToken}'
    // ACR names must be globally unique, alphanumeric only, 5-50 chars
    containerRegistryName: !empty(containerRegistryName) ? containerRegistryName : 'cr${resourceToken}'
    logAnalyticsWorkspaceName: !empty(logAnalyticsWorkspaceName) ? logAnalyticsWorkspaceName : 'log-${resourceToken}'
    managedEnvironmentName: !empty(managedEnvironmentName) ? managedEnvironmentName : 'cae-${resourceToken}'
    sqlConnectionString: sqlConnectionString
    azureAdTenantId: azureAdTenantId
    azureAdClientId: azureAdClientId
    azureAdInstance: azureAdInstance
  }
}

output AZURE_CONTAINER_REGISTRY_ENDPOINT string = resources.outputs.AZURE_CONTAINER_REGISTRY_ENDPOINT
output AZURE_CONTAINER_REGISTRY_NAME string = resources.outputs.AZURE_CONTAINER_REGISTRY_NAME
output AZURE_CONTAINER_APP_NAME string = resources.outputs.AZURE_CONTAINER_APP_NAME
output SERVICE_ENDPOINT string = resources.outputs.SERVICE_ENDPOINT
