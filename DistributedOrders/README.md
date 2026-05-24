# 🛒 Sistema Distribuído de Pedidos
**Projeto Acadêmico — 6º Período | Sistemas Distribuídos**

---

## 📐 Arquitetura

```
Cliente / Frontend
       ↓
 Azure API Gateway  (roteamento, autenticação, rate limiting)
    ↙      ↓      ↘
Order    Inventory  Notification
Service  Service    Service
  ↓          ↓           ↑
[SQL]      [SQL]         |
  ↓                      |
Redis ←──── Azure Service Bus ────────────────┘
(cache)   order-created / inventory-reserved / order-status-changed
```

---

## ✅ Premissas de Sistemas Distribuídos Aplicadas

| Premissa | Onde é aplicada no código |
|---|---|
| **Comunicação Assíncrona** | `OrderService` publica `OrderCreatedEvent` no Service Bus; `InventoryService` consome sem acoplamento direto |
| **Tolerância a Falhas** | `BackgroundService` com `AutoCompleteMessages=false` + `AbandonMessageAsync` (retry automático); `maxDeliveryCount=3` antes de dead-letter |
| **Consistência** | Transação com `UPDLOCK / ROWLOCK` no SQL para reserva de estoque sem race condition |
| **Transparência de Acesso** | Redis Cache camada de leitura: o cliente da API não sabe de onde vem o dado |
| **Escalabilidade Horizontal** | Cada microserviço é um container independente no Azure Container Apps com auto-scaling |
| **Independência de Serviços** | `NotificationService` não referencia nem conhece `OrderService` ou `InventoryService`; só consome eventos |
| **Observabilidade** | Application Insights em todos os serviços com logs estruturados e rastreamento por `CorrelationId` |
| **Replicação** | Service Bus garante entrega mesmo se um serviço estiver fora; dados ficam na fila |

---

## 📁 Estrutura de Pastas

```
DistributedOrders/
│
├── DistributedOrders.sln
│
├── shared/
│   └── SharedMessages/
│       ├── SharedMessages.csproj
│       └── Events.cs                   ← DTOs de eventos trocados entre serviços
│
├── src/
│   ├── OrderService/
│   │   ├── Controllers/
│   │   │   └── OrdersController.cs     ← REST API: GET/POST /api/orders
│   │   ├── Models/
│   │   │   └── Order.cs                ← Entidades Order e OrderItem
│   │   ├── Data/
│   │   │   └── OrderDbContext.cs       ← EF Core + Azure SQL
│   │   ├── Services/
│   │   │   └── OrderAppService.cs      ← Regras de negócio + publicação no Service Bus
│   │   ├── Consumers/
│   │   │   └── InventoryResponseConsumer.cs  ← Consome "inventory-reserved"
│   │   ├── Program.cs
│   │   ├── appsettings.json
│   │   └── Dockerfile
│   │
│   ├── InventoryService/
│   │   ├── Controllers/
│   │   │   └── InventoryController.cs  ← REST API: GET /api/inventory
│   │   ├── Models/
│   │   │   └── Product.cs              ← Entidades Product e StockReservation
│   │   ├── Data/
│   │   │   └── InventoryDbContext.cs   ← EF Core + Azure SQL
│   │   ├── Services/
│   │   │   └── InventoryAppService.cs  ← Lógica de reserva de estoque (com transação)
│   │   ├── Consumers/
│   │   │   └── OrderCreatedConsumer.cs ← Consome "order-created"
│   │   ├── Program.cs
│   │   ├── appsettings.json
│   │   └── Dockerfile
│   │
│   └── NotificationService/
│       ├── Services/
│       │   └── EmailService.cs         ← Simulação de envio de e-mail
│       ├── Consumers/
│       │   └── OrderStatusConsumer.cs  ← Consome "order-status-changed"
│       ├── Program.cs
│       ├── appsettings.json
│       └── Dockerfile
│
├── infra/
│   └── main.bicep                      ← IaC: provisiona todos os recursos na Azure
│
└── .github/
    └── workflows/
        └── deploy.yml                  ← CI/CD: build Docker + deploy Azure Container Apps
```

---

## 🚀 Como executar localmente

### Pré-requisitos
- .NET 8 SDK
- Docker Desktop
- Azure CLI (`az`)
- Uma conta Azure (gratuita serve para o lab)

### 1. Provisionar infraestrutura na Azure

```bash
az group create --name rg-distributed-orders --location eastus

az deployment group create \
  --resource-group rg-distributed-orders \
  --template-file infra/main.bicep
```

### 2. Preencher as connection strings

Edite os três arquivos `appsettings.json` com os valores gerados pelo Bicep:
- `OrderService/appsettings.json`
- `InventoryService/appsettings.json`
- `NotificationService/appsettings.json`

### 3. Rodar os serviços

```bash
# Terminal 1
cd src/OrderService && dotnet run

# Terminal 2
cd src/InventoryService && dotnet run

# Terminal 3
cd src/NotificationService && dotnet run
```

### 4. Testar o fluxo completo

```bash
# Criar um pedido (OrderService na porta 5001)
curl -X POST http://localhost:5001/api/orders \
  -H "Content-Type: application/json" \
  -d '{
    "customerId": "11111111-0000-0000-0000-000000000001",
    "customerEmail": "aluno@email.com",
    "items": [{
      "productId": "aaaaaaaa-0000-0000-0000-000000000001",
      "productName": "Notebook",
      "quantity": 1,
      "unitPrice": 4500.00
    }]
  }'

# O fluxo acontece automaticamente:
# 1. OrderService cria o pedido (status: Pending)
# 2. Publica OrderCreatedEvent no Service Bus
# 3. InventoryService consome e reserva o estoque
# 4. Publica InventoryReservedEvent
# 5. OrderService atualiza status (InventoryReserved)
# 6. Publica OrderStatusChangedEvent
# 7. NotificationService envia e-mail ao cliente

# Verificar o pedido atualizado
curl http://localhost:5001/api/orders/<ID_RETORNADO>
```

Também é possível acessar o Swagger de cada serviço:
- OrderService: http://localhost:5001/swagger
- InventoryService: http://localhost:5002/swagger

---

## ☁️ Deploy na Azure

O GitHub Actions em `.github/workflows/deploy.yml` faz o deploy automaticamente ao dar `push` na branch `main`.

Configure os secrets no repositório:
- `AZURE_CREDENTIALS` — output do comando `az ad sp create-for-rbac`

---

## 📚 Tecnologias utilizadas

| Tecnologia | Papel |
|---|---|
| **C# / .NET 8** | Linguagem e runtime |
| **Azure Service Bus** | Mensageria assíncrona entre microserviços |
| **Azure SQL** | Banco de dados relacional (um por serviço) |
| **Redis (Azure Cache)** | Cache distribuído para leitura de pedidos |
| **Azure Container Apps** | Hospedagem dos containers com auto-scaling |
| **Azure Application Insights** | Monitoramento e rastreamento distribuído |
| **EF Core** | ORM para acesso ao banco de dados |
| **Bicep** | Infrastructure as Code (IaC) |
| **Docker** | Containerização dos serviços |
| **GitHub Actions** | CI/CD pipeline |
