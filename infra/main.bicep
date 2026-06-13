// LoreBot — main infrastructure entry point
// See LOREBOT_SPEC.md for full architecture details

param location string = resourceGroup().location
param environment string = 'prod'

@secure()
param postgresAdminPassword string

@secure()
param openAiApiKey string = ''

module monitoring 'modules/monitoring.bicep' = {
  name: 'monitoring'
  params: { location: location, environment: environment }
}

module postgres 'modules/postgres.bicep' = {
  name: 'postgres'
  params: {
    location: location
    environment: environment
    adminPassword: postgresAdminPassword
  }
}

module functions 'modules/functions.bicep' = {
  name: 'functions'
  params: {
    location: location
    environment: environment
    appInsightsConnectionString: monitoring.outputs.appInsightsConnectionString
    openAiApiKey: openAiApiKey
    databaseConnectionString: 'Host=${postgres.outputs.fqdn};Database=lorebot;Username=lorebotadmin;Password=${postgresAdminPassword};SslMode=Require'
  }
}

module staticWebApp 'modules/staticwebapp.bicep' = {
  name: 'staticWebApp'
  params: { location: location, environment: environment }
}

output functionAppUrl string = functions.outputs.functionAppUrl
output staticWebAppHostname string = staticWebApp.outputs.defaultHostname
