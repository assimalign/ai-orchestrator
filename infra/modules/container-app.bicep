param name string
param location string
param managedEnvironmentId string
param image string
param containerName string = name
param registryServer string = 'ghcr.io'
param ingressEnabled bool = true
param ingressExternal bool = true
param targetPort int = 8080
param minReplicas int = 1
param maxReplicas int = 1
param cpu string = '0.5'
param memory string = '1Gi'
param env array = []
param secrets array = []
param scaleRules array = []

var registryEntries = empty(registryServer) ? [] : [
  {
    server: registryServer
  }
]

var secretEntries = [for secret in secrets: {
  name: secret.name
  value: secret.value
}]

var envEntries = [for item in env: contains(item, 'secretRef') ? {
  name: item.name
  secretRef: item.secretRef
} : {
  name: item.name
  value: string(item.value)
}]

var ingressConfig = ingressEnabled ? {
  ingress: {
    allowInsecure: false
    external: ingressExternal
    targetPort: targetPort
    transport: 'auto'
  }
} : {}

resource app 'Microsoft.App/containerApps@2024-03-01' = {
  name: name
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    managedEnvironmentId: managedEnvironmentId
    configuration: union({
      activeRevisionsMode: 'Single'
      registries: registryEntries
      secrets: secretEntries
    }, ingressConfig)
    template: {
      containers: [
        {
          name: containerName
          image: image
          env: envEntries
          resources: {
            cpu: json(cpu)
            memory: memory
          }
        }
      ]
      scale: {
        minReplicas: minReplicas
        maxReplicas: maxReplicas
        rules: scaleRules
      }
    }
  }
}

output id string = app.id
output fqdn string = ingressExternal ? app.properties.configuration.ingress.fqdn : ''
output principalId string = app.identity.principalId
