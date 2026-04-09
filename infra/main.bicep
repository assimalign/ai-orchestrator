targetScope = 'resourceGroup'

@description('Primary Azure region for all resources.')
param location string = resourceGroup().location

@description('Base name used for resource naming.')
param baseName string = 'ai-dev-orchestrator'

@description('Full GHCR image reference for the web frontend.')
param webImage string

@description('Full GHCR image reference for the API service.')
param apiImage string

@description('Full GHCR image reference for the worker service.')
param workerImage string

@description('GitHub App identifier used to access repositories. Leave empty when using a personal token.')
param githubAppId string = ''

@description('GitHub App installation identifier.')
param githubInstallationId string = ''

@description('Speech voice used by the web experience for playback.')
param speechVoice string = 'en-US-JennyNeural'

@description('Microsoft Entra tenant ID for MSAL sign-in and API token validation.')
param entraTenantId string = ''

@description('Microsoft Entra client ID for the SPA/API app registration.')
param entraClientId string = ''

@description('Microsoft Entra scope requested by the SPA when calling the API.')
param entraScope string = ''

@description('OpenAI model used for planning and synthesis.')
param openAiModel string = 'gpt-5.4'

@description('Anthropic model used for critique and review.')
param anthropicModel string = 'claude-sonnet-4-20250514'

var normalizedBase = toLower(replace(baseName, '-', ''))
var suffix = uniqueString(resourceGroup().id)
var logAnalyticsName = take('${baseName}-logs', 63)
var appInsightsName = take('${baseName}-appi', 260)
var containerAppEnvironmentName = take('${baseName}-env', 32)
var serviceBusNamespaceName = take('${normalizedBase}sb${suffix}', 50)
var queueName = 'orchestrator-runs'
var storageAccountName = take('${normalizedBase}st${suffix}', 24)
var keyVaultName = take('${normalizedBase}kv${suffix}', 24)
var speechServiceName = take('${normalizedBase}-speech-${suffix}', 64)
var apiAppName = take('${baseName}-api', 32)
var workerAppName = take('${baseName}-worker', 32)
var webAppName = take('${baseName}-web', 32)
var keyVaultSecretsUserRoleDefinitionId = subscriptionResourceId(
  'Microsoft.Authorization/roleDefinitions',
  '4633458b-17de-408a-b874-0445c86b69e6'
)

resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: logAnalyticsName
  location: location
  properties: {
    retentionInDays: 30
    sku: {
      name: 'PerGB2018'
    }
  }
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: appInsightsName
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalytics.id
  }
}

resource storage 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: storageAccountName
  location: location
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    accessTier: 'Hot'
    allowBlobPublicAccess: false
    minimumTlsVersion: 'TLS1_2'
    supportsHttpsTrafficOnly: true
  }
}

resource blobService 'Microsoft.Storage/storageAccounts/blobServices@2023-05-01' = {
  parent: storage
  name: 'default'
}

resource artifactsContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  parent: blobService
  name: 'artifacts'
}

resource serviceBus 'Microsoft.ServiceBus/namespaces@2024-01-01' = {
  name: serviceBusNamespaceName
  location: location
  sku: {
    name: 'Standard'
    tier: 'Standard'
  }
  properties: {
    publicNetworkAccess: 'Enabled'
  }
}

resource serviceBusQueue 'Microsoft.ServiceBus/namespaces/queues@2024-01-01' = {
  parent: serviceBus
  name: queueName
  properties: {
    defaultMessageTimeToLive: 'P14D'
    deadLetteringOnMessageExpiration: true
    duplicateDetectionHistoryTimeWindow: 'PT10M'
    requiresDuplicateDetection: false
  }
}

resource serviceBusRootAuthorizationRule 'Microsoft.ServiceBus/namespaces/AuthorizationRules@2024-01-01' existing = {
  parent: serviceBus
  name: 'RootManageSharedAccessKey'
}

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: keyVaultName
  location: location
  properties: {
    tenantId: subscription().tenantId
    enableRbacAuthorization: true
    enabledForTemplateDeployment: true
    publicNetworkAccess: 'Enabled'
    softDeleteRetentionInDays: 90
    sku: {
      family: 'A'
      name: 'standard'
    }
  }
}

resource speechService 'Microsoft.CognitiveServices/accounts@2024-10-01' = {
  name: speechServiceName
  location: location
  sku: {
    name: 'S0'
  }
  kind: 'SpeechServices'
  properties: {
    customSubDomainName: take('${normalizedBase}speech${suffix}', 64)
    publicNetworkAccess: 'Enabled'
  }
}

resource containerAppEnvironment 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: containerAppEnvironmentName
  location: location
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

var storageConnectionString = 'DefaultEndpointsProtocol=https;AccountName=${storage.name};AccountKey=${storage.listKeys().keys[0].value};EndpointSuffix=${environment().suffixes.storage}'
var serviceBusConnectionString = serviceBusRootAuthorizationRule.listKeys().primaryConnectionString
var keyVaultUrl = 'https://${keyVault.name}.${environment().suffixes.keyvaultDns}/'

module apiContainerApp 'modules/container-app.bicep' = {
  name: 'apiContainerApp'
  params: {
    name: apiAppName
    location: location
    managedEnvironmentId: containerAppEnvironment.id
    image: apiImage
    containerName: 'api'
    ingressExternal: true
    targetPort: 8080
    minReplicas: 1
    maxReplicas: 3
    cpu: '0.75'
    memory: '1.5Gi'
    secrets: [
      {
        name: 'storage-connection-string'
        value: storageConnectionString
      }
      {
        name: 'service-bus-connection-string'
        value: serviceBusConnectionString
      }
    ]
    env: [
      {
        name: 'PORT'
        value: 8080
      }
      {
        name: 'EXECUTION_MODE'
        value: 'servicebus'
      }
      {
        name: 'CORS_ORIGIN'
        value: '*'
      }
      {
        name: 'ENTRA_TENANT_ID'
        value: entraTenantId
      }
      {
        name: 'ENTRA_CLIENT_ID'
        value: entraClientId
      }
      {
        name: 'ENTRA_SCOPE'
        value: entraScope
      }
      {
        name: 'ORCHESTRATOR_TABLE_NAME'
        value: 'orchestratorstate'
      }
      {
        name: 'ORCHESTRATOR_QUEUE_NAME'
        value: queueName
      }
      {
        name: 'OPENAI_MODEL'
        value: openAiModel
      }
      {
        name: 'ANTHROPIC_MODEL'
        value: anthropicModel
      }
      {
        name: 'KEY_VAULT_URL'
        value: keyVaultUrl
      }
      {
        name: 'AZURE_SPEECH_REGION'
        value: location
      }
      {
        name: 'AZURE_SPEECH_KEY_SECRET_NAME'
        value: 'azure-speech-key'
      }
      {
        name: 'OPENAI_API_KEY_SECRET_NAME'
        value: 'openai-api-key'
      }
      {
        name: 'ANTHROPIC_API_KEY_SECRET_NAME'
        value: 'anthropic-api-key'
      }
      {
        name: 'GITHUB_TOKEN_SECRET_NAME'
        value: 'github-token'
      }
      {
        name: 'GITHUB_APP_PRIVATE_KEY_SECRET_NAME'
        value: 'github-app-private-key'
      }
      {
        name: 'GITHUB_APP_ID'
        value: githubAppId
      }
      {
        name: 'GITHUB_INSTALLATION_ID'
        value: githubInstallationId
      }
      {
        name: 'AZURE_STORAGE_CONNECTION_STRING'
        secretRef: 'storage-connection-string'
      }
      {
        name: 'SERVICE_BUS_CONNECTION_STRING'
        secretRef: 'service-bus-connection-string'
      }
    ]
  }
}

module workerContainerApp 'modules/container-app.bicep' = {
  name: 'workerContainerApp'
  params: {
    name: workerAppName
    location: location
    managedEnvironmentId: containerAppEnvironment.id
    image: workerImage
    containerName: 'worker'
    ingressEnabled: false
    ingressExternal: false
    targetPort: 8080
    minReplicas: 0
    maxReplicas: 5
    cpu: '0.75'
    memory: '1.5Gi'
    secrets: [
      {
        name: 'storage-connection-string'
        value: storageConnectionString
      }
      {
        name: 'service-bus-connection-string'
        value: serviceBusConnectionString
      }
    ]
    env: [
      {
        name: 'ORCHESTRATOR_TABLE_NAME'
        value: 'orchestratorstate'
      }
      {
        name: 'ORCHESTRATOR_QUEUE_NAME'
        value: queueName
      }
      {
        name: 'OPENAI_MODEL'
        value: openAiModel
      }
      {
        name: 'ANTHROPIC_MODEL'
        value: anthropicModel
      }
      {
        name: 'KEY_VAULT_URL'
        value: keyVaultUrl
      }
      {
        name: 'OPENAI_API_KEY_SECRET_NAME'
        value: 'openai-api-key'
      }
      {
        name: 'ANTHROPIC_API_KEY_SECRET_NAME'
        value: 'anthropic-api-key'
      }
      {
        name: 'GITHUB_TOKEN_SECRET_NAME'
        value: 'github-token'
      }
      {
        name: 'GITHUB_APP_PRIVATE_KEY_SECRET_NAME'
        value: 'github-app-private-key'
      }
      {
        name: 'GITHUB_APP_ID'
        value: githubAppId
      }
      {
        name: 'GITHUB_INSTALLATION_ID'
        value: githubInstallationId
      }
      {
        name: 'AZURE_STORAGE_CONNECTION_STRING'
        secretRef: 'storage-connection-string'
      }
      {
        name: 'SERVICE_BUS_CONNECTION_STRING'
        secretRef: 'service-bus-connection-string'
      }
    ]
    scaleRules: [
      {
        name: 'servicebus-queue'
        custom: {
          type: 'azure-servicebus'
          metadata: {
            messageCount: '1'
            namespace: serviceBus.name
            queueName: queueName
          }
          auth: [
            {
              secretRef: 'service-bus-connection-string'
              triggerParameter: 'connection'
            }
          ]
        }
      }
    ]
  }
}

module webContainerApp 'modules/container-app.bicep' = {
  name: 'webContainerApp'
  params: {
    name: webAppName
    location: location
    managedEnvironmentId: containerAppEnvironment.id
    image: webImage
    containerName: 'web'
    ingressExternal: true
    targetPort: 8080
    minReplicas: 1
    maxReplicas: 2
    cpu: '0.5'
    memory: '1Gi'
    env: [
      {
        name: 'API_BASE_URL'
        value: 'https://${apiContainerApp.outputs.fqdn}'
      }
      {
        name: 'ENTRA_TENANT_ID'
        value: entraTenantId
      }
      {
        name: 'ENTRA_CLIENT_ID'
        value: entraClientId
      }
      {
        name: 'ENTRA_SCOPE'
        value: entraScope
      }
      {
        name: 'SPEECH_VOICE'
        value: speechVoice
      }
    ]
  }
}

resource apiKeyVaultRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVault.id, apiAppName, 'kv-secrets-user')
  scope: keyVault
  properties: {
    principalId: apiContainerApp.outputs.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: keyVaultSecretsUserRoleDefinitionId
  }
}

resource workerKeyVaultRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVault.id, workerAppName, 'kv-secrets-user')
  scope: keyVault
  properties: {
    principalId: workerContainerApp.outputs.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: keyVaultSecretsUserRoleDefinitionId
  }
}

output apiUrl string = 'https://${apiContainerApp.outputs.fqdn}'
output webUrl string = 'https://${webContainerApp.outputs.fqdn}'
output keyVaultName string = keyVault.name
output storageAccountName string = storage.name
output serviceBusNamespace string = serviceBus.name
output speechServiceName string = speechService.name
output appInsightsConnectionString string = appInsights.properties.ConnectionString
