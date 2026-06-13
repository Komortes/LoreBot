param location string
param environment string

resource staticWebApp 'Microsoft.Web/staticSites@2023-01-01' = {
  name: 'lorebot-web-${environment}'
  location: location
  sku: { name: 'Free', tier: 'Free' }
  properties: {}
}

output defaultHostname string = staticWebApp.properties.defaultHostname
output deploymentToken string = staticWebApp.listSecrets().properties.apiKey
