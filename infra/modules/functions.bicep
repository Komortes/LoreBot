param location string
param environment string
param appInsightsConnectionString string

@secure()
param openAiApiKey string

@secure()
param databaseConnectionString string

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-01-01' = {
  name: 'lorebot${environment}sa'
  location: location
  sku: { name: 'Standard_LRS' }
  kind: 'Storage'
}

resource hostingPlan 'Microsoft.Web/serverfarms@2023-01-01' = {
  name: 'lorebot-plan-${environment}'
  location: location
  sku: { name: 'Y1', tier: 'Dynamic' }
  properties: {}
}

resource functionApp 'Microsoft.Web/sites@2023-01-01' = {
  name: 'lorebot-functions-${environment}'
  location: location
  kind: 'functionapp'
  properties: {
    serverFarmId: hostingPlan.id
    siteConfig: {
      appSettings: [
        { name: 'AzureWebJobsStorage', value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};AccountKey=${storageAccount.listKeys().keys[0].value}' }
        { name: 'FUNCTIONS_EXTENSION_VERSION', value: '~4' }
        { name: 'FUNCTIONS_WORKER_RUNTIME', value: 'dotnet-isolated' }
        { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value: appInsightsConnectionString }
        { name: 'OPENAI_API_KEY', value: openAiApiKey }
        { name: 'DATABASE_CONNECTION_STRING', value: databaseConnectionString }
      ]
      netFrameworkVersion: 'v10.0'
    }
    httpsOnly: true
  }
}

output functionAppName string = functionApp.name
output functionAppUrl string = 'https://${functionApp.properties.defaultHostName}'
