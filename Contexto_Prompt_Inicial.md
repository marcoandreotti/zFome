# Contexto

Atue como um **Arquiteto de Software Sênior e Desenvolvedor Full Stack especialista em .NET, C#, PostgreSQL, DDD, CQRS, Arquitetura Hexagonal, Clean Architecture, Vue.js e Quasar**.

Quero projetar e posteriormente implementar uma plataforma chamada provisoriamente de **zFome**, destinada ao controle de pedidos para:

- Bares
- Restaurantes
- Pizzarias
- Lanchonetes
- Hamburguerias
- Cafeterias
- Food trucks
- Outros estabelecimentos do setor alimentício

A solução deverá ser moderna, modular, escalável, testável, de fácil manutenção e preparada para evoluir futuramente para um modelo **SaaS Multi-Tenant**.

---

# Objetivo da plataforma

A plataforma deverá possuir três grandes áreas.

## 1. Área pública do cliente

Não deve exigir instalação de aplicativo.

O cliente deverá conseguir acessar o estabelecimento por:

- URL;
- QR Code;
- QR Code da mesa;
- link compartilhado;
- futuramente WhatsApp ou outras integrações.

Deverá ser possível:

- consultar o cardápio;
- navegar por categorias;
- visualizar produtos;
- visualizar imagens;
- visualizar adicionais;
- selecionar variações;
- montar combos;
- adicionar observações;
- adicionar/remover itens;
- visualizar carrinho;
- informar cupom;
- visualizar descontos;
- selecionar retirada, consumo local ou entrega;
- informar mesa/comanda quando aplicável;
- identificar-se;
- selecionar endereço;
- selecionar forma de pagamento;
- criar pedido;
- acompanhar andamento do pedido;
- solicitar alterações quando permitido;
- cancelar pedido quando permitido;
- repetir pedidos anteriores.

A experiência deverá ser extremamente simples, rápida e responsiva.

---

# 2. Área operacional

Área utilizada pela equipe do estabelecimento para administrar pedidos em tempo real.

Deverá permitir:

- visualizar novos pedidos;
- aceitar ou rejeitar pedidos;
- alterar status;
- acompanhar fila de produção;
- visualizar itens;
- visualizar observações;
- controlar tempo estimado;
- informar pedido em preparação;
- informar pedido pronto;
- informar pedido saiu para entrega;
- finalizar pedido;
- cancelar pedido;
- visualizar histórico;
- pesquisar pedidos;
- filtrar pedidos;
- imprimir pedidos;
- futuramente integrar com telas de cozinha/KDS.

Considere um fluxo de status como exemplo:

```text
Criado
  ↓
AguardandoConfirmacao
  ↓
Confirmado
  ↓
EmPreparacao
  ↓
Pronto
  ↓
SaiuParaEntrega
  ↓
Entregue
  ↓
Finalizado
```

Também deverão existir estados alternativos como:

```text
Cancelado
Rejeitado
```

O domínio deve controlar quais transições de estado são permitidas.

---

# 3. Área administrativa

Criar uma área administrativa completa para gerenciamento do estabelecimento.

Exemplos:

## Estabelecimento

- dados cadastrais;
- logotipo;
- cores;
- fontes;
- horários de funcionamento;
- taxas;
- configurações;
- formas de atendimento;
- configurações de pedido.

## Produtos

- produtos;
- categorias;
- subcategorias;
- imagens;
- preços;
- promoções;
- disponibilidade;
- estoque opcional;
- adicionais;
- grupos de adicionais;
- tamanhos;
- sabores;
- variações;
- combos.

Permitir estruturas recursivas quando fizer sentido.

Exemplo:

```text
Categoria
 ├── Subcategoria
 │    ├── Subcategoria
 │    │    └── Produto
```

Não limitar artificialmente a quantidade de níveis quando a modelagem recursiva for adequada.

## Cupons

Permitir regras como:

- percentual;
- valor fixo;
- valor mínimo do pedido;
- validade;
- limite de utilização;
- limite por cliente;
- produtos específicos;
- categorias específicas.

## Consultas

Criar consultas administrativas como:

- pedidos;
- faturamento;
- ticket médio;
- produtos mais vendidos;
- vendas por período;
- vendas por categoria;
- vendas por produto;
- cancelamentos;
- utilização de cupons;
- horários de maior movimento.

---

# Arquitetura geral

Utilizar:

- .NET 10;
- C#;
- Arquitetura Hexagonal;
- DDD;
- CQRS;
- Clean Architecture;
- SOLID;
- Clean Code.

A aplicação deverá ficar organizada dentro de **uma única Solution .NET**, ainda que existam diversos projetos internos.

Não utilizar:

- MediatR;
- AutoMapper.

Evitar bibliotecas desnecessárias.

Preferir implementações explícitas, simples, testáveis e controladas pela própria aplicação.

---

# Organização da Solution

Proponha inicialmente uma estrutura semelhante a:

```text
ZFome.sln

src/
 ├── ZFome.Api
 │
 ├── ZFome.Domain
 │
 ├── ZFome.Application
 │
 ├── ZFome.Infrastructure
 │
 ├── ZFome.Persistence.Write
 │
 ├── ZFome.Persistence.Read
 │
 ├── ZFome.CrossCutting
 │
 └── ZFome.Contracts

web/
 └── ZFome.Web

tests/
 ├── ZFome.Domain.Tests
 ├── ZFome.Application.Tests
 ├── ZFome.Integration.Tests
 └── ZFome.Architecture.Tests

docker/
scripts/
docs/

docker-compose.yml
README.md
```

A estrutura acima é apenas uma referência.

Analise criticamente e proponha alterações caso exista uma organização mais adequada à Arquitetura Hexagonal.

---

# Separação entre leitura e escrita

Aplicar CQRS de forma explícita.

## Escrita

Utilizar:

```text
Command
    ↓
CommandDispatcher
    ↓
CommandHandler
    ↓
Domain
    ↓
Repository
    ↓
Entity Framework Core
    ↓
PostgreSQL
```

## Leitura

Utilizar:

```text
Query
    ↓
QueryDispatcher
    ↓
QueryHandler
    ↓
Read Gateway
    ↓
Dapper
    ↓
PostgreSQL / SQLite
```

As consultas deverão retornar preferencialmente modelos próprios de leitura e não entidades do domínio.

---

# Gateways

Utilizar o conceito de **Gateway/Ports** da Arquitetura Hexagonal.

Separar claramente interfaces de leitura e escrita.

Exemplo conceitual:

```csharp
public interface IOrderWriteGateway
{
}

public interface IOrderReadGateway
{
}
```

Não criar abstrações sem necessidade.

Quando possível e tecnicamente adequado, criar abstrações genéricas reutilizáveis.

---

# Dispatchers próprios

Não utilizar MediatR.

Criar infraestrutura própria:

```text
ICommand
ICommand<TResult>

IQuery<TResult>

ICommandHandler<TCommand>
ICommandHandler<TCommand, TResult>

IQueryHandler<TQuery, TResult>

ICommandDispatcher
IQueryDispatcher
```

Exemplo:

```csharp
await commandDispatcher.DispatchAsync(command);

var result =
    await queryDispatcher.DispatchAsync(query);
```

Os Dispatchers deverão resolver os handlers através do container nativo de Dependency Injection do .NET.

---

# FluentValidation

Utilizar **FluentValidation**.

A validação deverá acontecer automaticamente antes da execução dos handlers.

Fluxo desejado:

```text
Request
   ↓
Dispatcher
   ↓
FluentValidation
   ↓
Handler
```

Quando a validação falhar, lançar uma exceção padronizada que será tratada pelo middleware global.

Não duplicar validações de regra de negócio que pertencem ao domínio.

---

# Generics

Sempre avaliar a possibilidade de reutilização através de generics.

Por exemplo:

```text
Repository<TEntity>
ReadGateway<TResult>
CommandHandler<TCommand>
QueryHandler<TQuery, TResult>
PagedResult<T>
Result<T>
```

Porém:

> Não criar abstrações genéricas simplesmente para reduzir código.

A genericidade deve ser utilizada somente quando representar um comportamento realmente comum.

Evitar **overengineering**.

---

# Persistência de escrita

Utilizar:

- PostgreSQL;
- Entity Framework Core;
- migrations;
- Unit of Work quando fizer sentido;
- transactions.

O EF Core deverá ser utilizado principalmente para operações que alterem estado.

Exemplo:

```text
INSERT
UPDATE
DELETE
```

Não utilizar Repository Pattern apenas como uma cópia dos métodos do DbSet.

Os repositories devem possuir métodos relacionados ao domínio.

Exemplo:

```csharp
GetOrderForUpdateAsync(...)
AddAsync(...)
```

---

# Persistência de leitura

Utilizar Dapper.

As consultas deverão ser otimizadas para leitura.

Não obrigar que o modelo de leitura possua a mesma estrutura do domínio.

Permitir:

- joins;
- projections;
- CTE;
- consultas agregadas;
- paginação;
- filtros dinâmicos.

---

# SQLite

Utilizar SQLite quando houver vantagem arquitetural clara.

Avaliar usos como:

- cache local;
- leitura local;
- funcionamento offline;
- informações temporárias;
- ambientes de demonstração/desenvolvimento.

Não utilizar SQLite apenas porque foi solicitado.

Explique onde ele realmente agrega valor à arquitetura.

---

# Multi-Tenant

Projetar a solução desde o início preparada para múltiplos estabelecimentos.

Exemplo:

```text
Tenant
 ├── Restaurante A
 ├── Restaurante B
 └── Pizzaria C
```

As principais entidades deverão possuir identificação do Tenant quando aplicável.

Avaliar estratégias como:

```text
TenantId
```

em todas as estruturas pertencentes a um estabelecimento.

Garantir isolamento dos dados.

Nenhum Tenant poderá acessar informações de outro Tenant.

---

# Segurança

Implementar autenticação e autorização.

Utilizar inicialmente:

- JWT;
- Access Token;
- Refresh Token;
- Roles;
- Claims;
- Policies.

Perfis possíveis:

```text
Owner
Administrator
Manager
Attendant
Kitchen
Delivery
Customer
```

Utilizar autorização baseada em Policies quando adequado.

Não confiar em TenantId recebido diretamente do front-end para decisões de segurança.

---

# API

Criar API REST utilizando ASP.NET Core.

Implementar:

- Controllers;
- DTOs;
- Commands;
- Queries;
- versionamento;
- autenticação;
- autorização;
- documentação;
- health checks.

Evitar regras de negócio dentro dos Controllers.

Controller deve ser fino.

Exemplo:

```text
Controller
    ↓
CommandDispatcher / QueryDispatcher
    ↓
Application
```

---

# Exception Handling

Criar Middleware global de tratamento de exceções.

Exemplo:

```text
GlobalExceptionMiddleware
```

Centralizar o tratamento de:

```text
ValidationException
DomainException
NotFoundException
UnauthorizedException
ForbiddenException
ConflictException
InfrastructureException
```

Utilizar `ProblemDetails` seguindo padrões HTTP.

Não espalhar `try/catch` pelos Controllers.

---

# Swagger / OpenAPI

Adicionar:

- Swagger;
- OpenAPI;
- autenticação JWT no Swagger;
- documentação dos endpoints;
- exemplos de requests;
- exemplos de responses;
- códigos HTTP documentados.

---

# Idempotência

Pedidos e pagamentos são operações críticas.

Preparar mecanismos para evitar duplicidade causada por:

- duplo clique;
- timeout;
- retry;
- problemas de conexão.

Avaliar implementação de:

```text
Idempotency-Key
```

principalmente para:

```text
POST /orders
POST /payments
```

---

# Auditoria

Registrar operações importantes.

Exemplos:

```text
Pedido criado
Pedido alterado
Pedido cancelado
Cupom utilizado
Produto alterado
Usuário alterado
Configuração alterada
```

Registrar quando aplicável:

- Tenant;
- usuário;
- data/hora;
- operação;
- entidade;
- identificador;
- estado anterior;
- novo estado.

---

# Cache

Avaliar uso futuro de Redis para:

- catálogo;
- cardápio;
- sessões;
- configurações;
- consultas frequentes;
- controle distribuído;
- idempotência.

Não tornar Redis obrigatório na primeira versão caso não seja necessário.

Criar arquitetura preparada para adicioná-lo posteriormente.

---

# Observabilidade

Adicionar suporte a:

- logging estruturado;
- Serilog;
- correlation ID;
- tracing;
- métricas;
- health checks.

Preparar arquitetura para OpenTelemetry.

Cada requisição deverá poder ser rastreada entre as camadas.

---

# Testes

Criar:

## Unit Tests

Principalmente:

```text
Domain
Application
Validators
Handlers
```

Utilizar:

- xUnit;
- FluentAssertions.

## Integration Tests

Testar:

- PostgreSQL;
- repositories;
- Dapper;
- API;
- autenticação.

Avaliar uso de Testcontainers.

## Architecture Tests

Adicionar testes que impeçam dependências arquiteturais inválidas.

Exemplo:

```text
Domain não pode depender de Infrastructure.

Application não pode depender da API.

Read não pode depender de Write.
```

---

# Front-end

Utilizar:

- Vue.js;
- Quasar Framework;
- Composition API;
- TypeScript.

Criar uma organização modular e escalável.

Exemplo:

```text
src/
 ├── boot/
 ├── components/
 ├── composables/
 ├── layouts/
 ├── pages/
 ├── router/
 ├── services/
 ├── stores/
 ├── models/
 ├── modules/
 └── themes/
```

Utilizar Pinia quando gerenciamento de estado global for necessário.

---

# Design System

Não espalhar cores, fontes, tamanhos ou estilos diretamente nos componentes.

Criar estrutura central de temas.

Permitir que cada estabelecimento configure:

- cor principal;
- cor secundária;
- background;
- tipografia;
- logotipo;
- bordas;
- identidade visual.

Exemplo:

```text
TenantTheme
```

A interface pública deverá conseguir assumir a identidade visual de cada estabelecimento.

---

# Componentização

Criar componentes reutilizáveis sempre que houver comportamento comum.

Exemplos:

```text
AppButton
AppInput
AppDialog
AppCard
AppTable
AppPagination
ProductCard
CategoryTree
PriceDisplay
OrderStatus
OrderTimeline
CartItem
```

Evitar componentes gigantes.

---

# Recursividade

Utilizar componentes recursivos quando fizer sentido.

Principalmente para:

- categorias;
- adicionais;
- menus;
- estruturas hierárquicas.

Exemplo:

```vue
<CategoryNode
  v-for="category in categories"
  :category="category"
/>
```

onde `CategoryNode` pode renderizar outros `CategoryNode`.

---

# Responsividade

Priorizar experiência mobile.

Aplicar abordagem:

```text
Mobile First
```

A área pública do cliente deve funcionar perfeitamente em smartphone.

Também deverá funcionar em:

- tablet;
- desktop.

---

# PWA

Preparar a aplicação para possibilidade de utilização como **Progressive Web App**.

Avaliar:

- instalação opcional;
- cache;
- funcionamento parcial offline;
- notificações.

---

# Agentes de IA

Projetar uma camada para integração futura com agentes de IA.

Não permitir que agentes tenham acesso direto ao banco de dados.

Criar abstrações como:

```text
IAgent
IAgentContext
IAgentTool
IAgentDispatcher
```

Os agentes deverão executar operações através de ferramentas controladas pela aplicação.

Possíveis agentes:

## Agente de atendimento

Pode ajudar o cliente a:

- encontrar produtos;
- consultar ingredientes;
- identificar promoções;
- sugerir combinações;
- responder dúvidas.

## Agente de pedidos

Pode interpretar frases como:

```text
"Quero uma pizza grande de calabresa sem cebola
e uma Coca-Cola de 2 litros."
```

e transformar em uma proposta estruturada de carrinho.

O cliente sempre deverá confirmar antes da criação definitiva do pedido.

## Agente administrativo

Pode responder:

```text
"Quanto vendemos hoje?"

"Quais foram os 10 produtos mais vendidos esta semana?"

"Qual horário vende mais?"

"Quantos pedidos foram cancelados este mês?"
```

Os agentes deverão respeitar:

- Tenant;
- usuário;
- permissões;
- contexto;
- auditoria.

---

# Integrações futuras

Preparar Ports/Adapters para futuras integrações.

Exemplos:

```text
PaymentGateway
WhatsAppGateway
EmailGateway
SmsGateway
PrinterGateway
DeliveryGateway
StorageGateway
AiGateway
NotificationGateway
```

Nunca acoplar o domínio diretamente ao fornecedor externo.

Por exemplo:

```text
IPaymentGateway
```

poderá posteriormente possuir:

```text
MercadoPagoPaymentGateway
StripePaymentGateway
PagSeguroPaymentGateway
```

---

# Eventos de domínio

Utilizar Domain Events quando realmente houver benefício.

Exemplo:

```text
OrderCreated
OrderConfirmed
OrderCancelled
OrderReady
OrderDelivered
PaymentConfirmed
```

Evitar transformar tudo em eventos.

---

# Evolução futura

Preparar a solução para futuramente suportar:

- Outbox Pattern;
- mensageria;
- RabbitMQ/Kafka;
- processamento assíncrono;
- notificações em tempo real;
- WebSockets/SignalR;
- Redis;
- integração com WhatsApp;
- pagamentos online;
- impressoras térmicas;
- Kitchen Display System;
- delivery;
- aplicativos mobile;
- BI;
- agentes de IA.

Não implementar infraestrutura complexa antecipadamente.

A arquitetura deve permitir a evolução sem obrigar sua utilização na primeira versão.

---

# Docker

Toda a solução deverá poder ser executada através do Docker.

Criar:

```text
Dockerfile API
Dockerfile Web
docker-compose.yml
```

Inicialmente considerar:

```text
PostgreSQL
API .NET
Vue/Quasar
```

Adicionar outros serviços somente quando necessários.

Criar volumes persistentes para PostgreSQL.

Configurações e credenciais deverão utilizar:

```text
.env
environment variables
Docker secrets quando aplicável
```

Nunca versionar senhas reais.

---

# Configuração

Utilizar:

```text
appsettings.json
appsettings.Development.json
Environment Variables
Options Pattern
```

Utilizar classes fortemente tipadas para configurações.

---

# Princípios obrigatórios

Durante toda a implementação:

1. Não utilizar MediatR.
2. Não utilizar AutoMapper.
3. Não colocar regra de negócio em Controller.
4. Não retornar entidades diretamente pela API.
5. Não colocar regra de negócio em Repository.
6. Não acoplar Domain ao Entity Framework.
7. Não acoplar Domain ao Dapper.
8. Não acoplar Domain a serviços externos.
9. Não criar abstrações sem necessidade.
10. Não criar microservices neste momento.
11. Preferir Modular Monolith.
12. Manter separação clara entre Read e Write.
13. Priorizar legibilidade sobre código excessivamente sofisticado.
14. Utilizar código moderno e idiomático do C#/.NET.
15. Aplicar async/await nas operações de I/O.
16. Utilizar CancellationToken.
17. Utilizar nullable reference types.
18. Utilizar records quando apropriado.
19. Preferir imutabilidade onde fizer sentido.
20. Manter baixo acoplamento entre módulos.

---

# Estratégia arquitetural

Começar como um:

> **Modular Monolith**

Não utilizar microservices prematuramente.

Organizar os contextos para que possam futuramente ser separados caso exista necessidade.

Possíveis bounded contexts:

```text
Identity
Tenancy
Catalog
Orders
Customers
Coupons
Payments
Delivery
Notifications
Reporting
AI
```

Avalie quais realmente devem existir na primeira versão.

---

# Primeira tarefa

**Não comece implementando código imediatamente.**

Primeiramente quero que você atue como arquiteto.

Execute estas etapas:

### Etapa 1 — Análise

Analise todos os requisitos e identifique:

- requisitos funcionais;
- requisitos não funcionais;
- riscos técnicos;
- decisões arquiteturais importantes;
- pontos que ainda precisam ser definidos.

### Etapa 2 — Arquitetura

Proponha a arquitetura completa da solução utilizando:

```text
Hexagonal Architecture
DDD
CQRS
Clean Architecture
Modular Monolith
```

Explique claramente como esses conceitos serão combinados sem criar complexidade desnecessária.

### Etapa 3 — Solution

Proponha a árvore completa da Solution:

```text
ZFome.sln
src/
web/
tests/
docker/
docs/
```

Mostrando:

- projetos;
- pastas;
- responsabilidades;
- dependências permitidas.

### Etapa 4 — Fluxo CQRS

Demonstre com diagramas textuais:

```text
HTTP → Controller → Dispatcher → Validator → Handler
```

para Command e Query.

### Etapa 5 — Domínio

Proponha inicialmente:

- Bounded Contexts;
- Aggregates;
- Entities;
- Value Objects;
- Domain Services;
- Domain Events.

Explique a responsabilidade de cada um.

### Etapa 6 — Banco

Proponha:

- estrutura inicial PostgreSQL;
- separação Read/Write;
- uso do EF Core;
- uso do Dapper;
- estratégia Multi-Tenant;
- migrations;
- indexes importantes.

### Etapa 7 — Front-end

Proponha arquitetura Vue + Quasar incluindo:

- módulos;
- pages;
- layouts;
- components;
- composables;
- stores;
- services;
- themes;
- gerenciamento de estado;
- autenticação;
- comunicação com a API.

### Etapa 8 — Docker

Descreva a topologia:

```text
Browser
   │
   ▼
Vue / Quasar
   │
   ▼
.NET API
   │
   ├── PostgreSQL
   └── serviços futuros
```

### Etapa 9 — Roadmap

Divida a implementação em fases incrementais.

Exemplo:

```text
Fase 0 — Fundação arquitetural
Fase 1 — Tenant + autenticação
Fase 2 — Catálogo
Fase 3 — Carrinho
Fase 4 — Pedido
Fase 5 — Operação
Fase 6 — Administração
Fase 7 — Cupons
Fase 8 — Relatórios
Fase 9 — IA
Fase 10 — Integrações
```

Cada fase deve gerar uma aplicação executável e testável.

---

# Forma de trabalho

Ao implementar posteriormente:

1. trabalhar uma fase por vez;
2. explicar a decisão arquitetural antes de gerar estruturas grandes;
3. gerar código completo e compilável;
4. informar o caminho de cada arquivo criado;
5. não omitir namespaces;
6. não utilizar pseudocódigo quando estivermos na etapa de implementação;
7. criar testes junto com funcionalidades importantes;
8. atualizar README e documentação quando a arquitetura mudar;
9. não alterar decisões arquiteturais anteriores silenciosamente;
10. apontar claramente qualquer decisão que precise ser revista.

Quando houver mais de uma solução tecnicamente válida, apresente:

```text
Opção A
Opção B
Recomendação
Motivo
```

Priorize sempre:

> simplicidade + manutenção + testabilidade + evolução futura.

A arquitetura deve ser robusta, mas não quero **overengineering**.