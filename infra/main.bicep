// LoreBot — main infrastructure entry point
// See LOREBOT_SPEC.md for full architecture details

param location string = resourceGroup().location
param environment string = 'prod'

module monitoring 'modules/monitoring.bicep' = {
  name: 'monitoring'
  params: { location: location, environment: environment }
}

module postgres 'modules/postgres.bicep' = {
  name: 'postgres'
  params: { location: location, environment: environment }
}

module functions 'modules/functions.bicep' = {
  name: 'functions'
  params: {
    location: location
    environment: environment
    appInsightsConnectionString: monitoring.outputs.appInsightsConnectionString
  }
}

module staticWebApp 'modules/staticwebapp.bicep' = {
  name: 'staticWebApp'
  params: { location: location, environment: environment }
}
