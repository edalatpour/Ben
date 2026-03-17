// New resource-group-scoped module for Azure Container Apps deployment.
// Does NOT contain any App Service resources — the existing resources.bicep is unchanged.
targetScope = 'resourceGroup'

@minLength(1)
@description('Primary location for all resources.')
param location string

@description('The name of the Container App.')
param containerAppName string

@description('The name of the Azure Container Registry.')
param containerRegistryName string

@description('The name of the Log Analytics Workspace.')
param logAnalyticsWorkspaceName string

@description('The name of the ACA Managed Environment.')
param managedEnvironmentName string

@description('SQL Server connection string passed as a secret into the container.')
@secure()
param sqlConnectionString string

@description('Azure AD / Entra ID Tenant ID used for JWT bearer validation.')
param azureAdTenantId string

@description('Azure AD / Entra ID Client ID (App Registration) used for JWT bearer validation.')
param azureAdClientId string

@description('Azure AD / Entra ID authority base URL.')
param azureAdInstance string = 'https://login.microsoftonline.com/'

@description('Tags applied to all resources.')
param tags object = {}

// ---------------------------------------------------------------------------
// Log Analytics Workspace
// ---------------------------------------------------------------------------
resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2022-10-01' = {
  name: logAnalyticsWorkspaceName
  location: location
  tags: tags
  properties: {
    sku: { name: 'PerGB2018' }
    retentionInDays: 30
  }
}

// ---------------------------------------------------------------------------
// Azure Container Registry (Basic SKU — cheapest tier)
// ---------------------------------------------------------------------------
resource containerRegistry 'Microsoft.ContainerRegistry/registries@2023-01-01-preview' = {
  name: containerRegistryName
  location: location
  tags: tags
  sku: { name: 'Basic' }
  properties: {
    adminUserEnabled: false
  }
}

// ---------------------------------------------------------------------------
// User-assigned managed identity for the Container App to pull from ACR
// ---------------------------------------------------------------------------
resource managedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: 'id-${containerAppName}'
  location: location
  tags: tags
}

// AcrPull built-in role
var acrPullRoleId = subscriptionResourceId(
  'Microsoft.Authorization/roleDefinitions',
  '7f951dda-4ed3-4680-a7ca-43fe172d538d'
)

resource acrPullAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(containerRegistry.id, managedIdentity.id, acrPullRoleId)
  scope: containerRegistry
  properties: {
    principalId: managedIdentity.properties.principalId
    roleDefinitionId: acrPullRoleId
    principalType: 'ServicePrincipal'
  }
}

// ---------------------------------------------------------------------------
// ACA Managed Environment
// ---------------------------------------------------------------------------
resource managedEnvironment 'Microsoft.App/managedEnvironments@2023-05-01' = {
  name: managedEnvironmentName
  location: location
  tags: tags
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logAnalytics.properties.customerId
        sharedKey: logAnalytics.listKeys().primarySharedKey
      }
    }
  }
}

// ---------------------------------------------------------------------------
// Container App
// ---------------------------------------------------------------------------
resource containerApp 'Microsoft.App/containerApps@2023-05-01' = {
  name: containerAppName
  location: location
  // azd uses 'azd-service-name' tag to locate the container app to deploy to
  tags: union(tags, { 'azd-service-name': 'backend' })
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${managedIdentity.id}': {}
    }
  }
  properties: {
    managedEnvironmentId: managedEnvironment.id
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        external: true
        targetPort: 8080   // .NET 10 runtime image default non-root port
        transport: 'http'
        allowInsecure: false
      }
      registries: [
        {
          server: containerRegistry.properties.loginServer
          identity: managedIdentity.id
        }
      ]
      secrets: [
        {
          name: 'sql-connection-string'
          value: sqlConnectionString
        }
      ]
    }
    template: {
      containers: [
        {
          // Placeholder image — azd replaces this on first `azd deploy`
          name: 'backend'
          image: 'mcr.microsoft.com/azuredocs/containerapps-helloworld:latest'
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }
          probes: [
            {
              type: 'Liveness'
              httpGet: {
                path: '/health'
                port: 8080
              }
              initialDelaySeconds: 30
              periodSeconds: 30
            }
            {
              type: 'Readiness'
              httpGet: {
                path: '/health'
                port: 8080
              }
              initialDelaySeconds: 15
              periodSeconds: 10
            }
          ]
          env: [
            {
              name: 'ASPNETCORE_ENVIRONMENT'
              value: 'Production'
            }
            {
              name: 'ASPNETCORE_URLS'
              value: 'http://+:8080'
            }
            {
              // Injected from the ACA secret — never appears in plain text in config
              name: 'ConnectionStrings__DefaultConnection'
              secretRef: 'sql-connection-string'
            }
            {
              name: 'AzureAd__Instance'
              value: azureAdInstance
            }
            {
              name: 'AzureAd__TenantId'
              value: azureAdTenantId
            }
            {
              name: 'AzureAd__ClientId'
              value: azureAdClientId
            }
            {
              name: 'AzureAd__Authority'
              value: '${azureAdInstance}${azureAdTenantId}/v2.0'
            }
            {
              name: 'AzureAd__Audience'
              value: azureAdClientId
            }
          ]
        }
      ]
      scale: {
        minReplicas: 0   // Scale to zero when idle
        maxReplicas: 3
      }
    }
  }
  dependsOn: [acrPullAssignment]
}

// ---------------------------------------------------------------------------
// Outputs consumed by azd
// ---------------------------------------------------------------------------
output AZURE_CONTAINER_REGISTRY_ENDPOINT string = containerRegistry.properties.loginServer
output AZURE_CONTAINER_REGISTRY_NAME string = containerRegistry.name
output AZURE_CONTAINER_APP_NAME string = containerApp.name
output SERVICE_ENDPOINT string = 'https://${containerApp.properties.configuration.ingress.fqdn}'
