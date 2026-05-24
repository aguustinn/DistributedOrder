// infra/main.bicep — Provisiona todos os recursos na Azure
// Deploy: az deployment group create --resource-group rg-distributed-orders --template-file infra/main.bicep

@description('Prefixo para nomear todos os recursos')
param prefix string = 'distorders'

@description('Região Azure')
param location string = resourceGroup().location

// ── Azure SQL Server ──────────────────────────────────────────────────────────
resource sqlServer 'Microsoft.Sql/servers@2023-05-01-preview' = {
  name: '${prefix}-sql'
  location: location
  properties: {
    administratorLogin: 'sqladmin'
    administratorLoginPassword: 'SuaSenha@123!'   // em produção: use Key Vault
  }
}

resource sqlFirewall 'Microsoft.Sql/servers/firewallRules@2023-05-01-preview' = {
  parent: sqlServer
  name: 'AllowAzureServices'
  properties: { startIpAddress: '0.0.0.0', endIpAddress: '0.0.0.0' }
}

resource ordersDb 'Microsoft.Sql/servers/databases@2023-05-01-preview' = {
  parent: sqlServer
  name: 'OrdersDb'
  location: location
  sku: { name: 'Basic', tier: 'Basic' }
}

resource inventoryDb 'Microsoft.Sql/servers/databases@2023-05-01-preview' = {
  parent: sqlServer
  name: 'InventoryDb'
  location: location
  sku: { name: 'Basic', tier: 'Basic' }
}

// ── Azure Service Bus ─────────────────────────────────────────────────────────
resource serviceBus 'Microsoft.ServiceBus/namespaces@2022-10-01-preview' = {
  name: '${prefix}-sb'
  location: location
  sku: { name: 'Standard', tier: 'Standard' }
}

resource queueOrderCreated 'Microsoft.ServiceBus/namespaces/queues@2022-10-01-preview' = {
  parent: serviceBus
  name: 'order-created'
  properties: {
    maxDeliveryCount: 3          // 3 tentativas antes do dead-letter
    lockDuration: 'PT5M'
    defaultMessageTimeToLive: 'P1D'
  }
}

resource queueInventoryReserved 'Microsoft.ServiceBus/namespaces/queues@2022-10-01-preview' = {
  parent: serviceBus
  name: 'inventory-reserved'
  properties: { maxDeliveryCount: 3, lockDuration: 'PT5M' }
}

resource queueStatusChanged 'Microsoft.ServiceBus/namespaces/queues@2022-10-01-preview' = {
  parent: serviceBus
  name: 'order-status-changed'
  properties: { maxDeliveryCount: 3, lockDuration: 'PT5M' }
}

// ── Redis Cache ───────────────────────────────────────────────────────────────
resource redis 'Microsoft.Cache/redis@2023-08-01' = {
  name: '${prefix}-redis'
  location: location
  properties: {
    sku: { name: 'Basic', family: 'C', capacity: 0 }
    enableNonSslPort: false
  }
}

// ── Application Insights ──────────────────────────────────────────────────────
resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: '${prefix}-insights'
  location: location
  kind: 'web'
  properties: { Application_Type: 'web' }
}

// ── Azure Container Apps Environment ─────────────────────────────────────────
resource logWorkspace 'Microsoft.OperationalInsights/workspaces@2022-10-01' = {
  name: '${prefix}-logs'
  location: location
  properties: { sku: { name: 'PerGB2018' } }
}

resource containerEnv 'Microsoft.App/managedEnvironments@2023-05-01' = {
  name: '${prefix}-env'
  location: location
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logWorkspace.properties.customerId
        sharedKey: logWorkspace.listKeys().primarySharedKey
      }
    }
  }
}

// ── Outputs ───────────────────────────────────────────────────────────────────
output sqlServerFqdn string = sqlServer.properties.fullyQualifiedDomainName
output serviceBusEndpoint string = serviceBus.properties.serviceBusEndpoint
output redisFqdn string = redis.properties.hostName
output appInsightsKey string = appInsights.properties.InstrumentationKey
output containerEnvId string = containerEnv.id
