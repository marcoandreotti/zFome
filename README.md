<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="web/ZFome.Web/public/brand/zfome-logo-dark.png" />
    <source media="(prefers-color-scheme: light)" srcset="web/ZFome.Web/public/brand/zfome-logo.png" />
    <img src="web/ZFome.Web/public/brand/zfome-logo.png" alt="zFome" width="240" />
  </picture>
</p>

<p align="center">
  <strong>Seu estabelecimento, seus pedidos, seu ritmo.</strong><br />
  Plataforma SaaS multi-tenant para transformar cardápio, pedidos e operação em um único fluxo.
</p>

<p align="center">
  <img src="https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white" alt=".NET 10" />
  <img src="https://img.shields.io/badge/Vue.js-Quasar-41B883?logo=vuedotjs&logoColor=white" alt="Vue.js e Quasar" />
  <img src="https://img.shields.io/badge/PostgreSQL--CQRS-4169E1?logo=postgresql&logoColor=white" alt="PostgreSQL e CQRS" />
  <img src="https://img.shields.io/badge/Arquitetura-Modular%20Monolith-222222" alt="Monólito modular" />
</p>

<p align="center">
  <a href="#visão-do-produto">Produto</a> ·
  <a href="#arquitetura">Arquitetura</a> ·
  <a href="#executando-localmente">Executar</a> ·
  <a href="#documentação">Documentação</a>
</p>

---

## O que é o zFome?

O zFome é uma plataforma para bares, restaurantes, pizzarias, lanchonetes e outros estabelecimentos de alimentação gerenciarem sua operação digital.

Ele conecta:

- cardápio público e descoberta por cliente;
- criação e acompanhamento de pedidos;
- retirada, entrega e pedidos agendados;
- administração do estabelecimento;
- operação da cozinha e atendimento;
- avaliações e reputação;
- notificações e comunicação;
- configuração multi-tenant com isolamento por estabelecimento.

O projeto está sendo construído como um **monólito modular**, priorizando evolução segura, testes, manutenção e clareza de domínio antes de qualquer extração para serviços independentes.

> O zFome é um projeto de engenharia aplicada: combina domínio de pedidos, operação de cozinha, multi-tenancy, CQRS, persistência especializada, comunicação assíncrona e evolução arquitetural incremental em uma única plataforma.

<p align="center">
  <img src="docs/assets/zfome-knowledge-brain.png" alt="Ilustração do Cérebro do zFome, um mapa de conhecimento conectado por domínios" width="900" />
</p>

<p align="center">
  <em>O Cérebro do zFome conecta produto, domínio, arquitetura, decisões e orientações de desenvolvimento.</em>
</p>

## Visão do produto

```mermaid
flowchart LR
    C[Cliente] --> P[Cardápio público]
    P --> O[Pedido]
    O --> A[Administração]
    O --> K[Operações e cozinha]
    K --> S[Retirada ou entrega]
    S --> T[Acompanhamento do cliente]
    T --> R[Avaliação e reputação]
```

### Superfícies da aplicação

| Superfície | Objetivo |
| --- | --- |
| Public | Descoberta, cardápio, carrinho, checkout e acompanhamento do cliente. |
| Administration | Configuração do estabelecimento, catálogo, disponibilidade, usuários e relatórios. |
| Operations | Acompanhamento rápido de pedidos, preparação, retirada e entrega. |
| KDS | Leitura à distância e execução do trabalho da cozinha. |
| Platform | Provisionamento e contexto institucional da plataforma. |

## Em destaque

- **Cardápio digital** com categorias, produtos, imagens, preços e composição.
- **Pedidos em tempo real** com estados claros para cliente, atendimento e cozinha.
- **Pedidos agendados** com janela de preparo e regras de antecedência.
- **Acompanhamento público** sem exigir autenticação do cliente nesta fase.
- **Avaliações** vinculadas ao pedido, com publicação e moderação previstas no domínio.
- **Multi-tenancy** com isolamento de dados por estabelecimento.
- **Tema claro e escuro** persistido no navegador.
- **Notificações locais** através do Mailpit durante o desenvolvimento.

## Arquitetura

O zFome utiliza .NET 10 no backend e Vue/Quasar no frontend, seguindo Clean Architecture, DDD, CQRS e princípios de arquitetura hexagonal.

```mermaid
flowchart TB
    WEB[Vue + Quasar + TypeScript]
    API[ZFome.Api\nControllers finos]
    APP[ZFome.Application\nCommands, Queries e handlers]
    DOMAIN[ZFome.Domain\nAgregados e regras]
    WRITE[EF Core\nWrite Gateway]
    READ[Dapper\nRead Gateway]
    DB[(PostgreSQL)]

    WEB --> API
    API --> APP
    APP --> DOMAIN
    APP --> WRITE
    APP --> READ
    WRITE --> DB
    READ --> DB
```

### Monólito modular e mensageria

O zFome não começa como um conjunto prematuro de microservices. Os módulos compartilham o processo e o PostgreSQL, mas preservam limites explícitos entre Domain, Application, Infrastructure, Contracts e API.

Para tarefas assíncronas e integrações, o projeto utiliza uma **Transactional Outbox** como fila persistente do monólito:

```mermaid
flowchart LR
    CMD[Command / caso de uso] --> TX{Transação PostgreSQL}
    TX --> BIZ[Dados do negócio]
    TX --> OUT[(integration.outbox_message)]
    OUT --> WORKER[OutboxProcessingWorker]
    WORKER --> RETRY[Retry, lease e idempotência]
    RETRY --> HANDLER[Handlers de integração]
```

Isso garante que a alteração de negócio e a mensagem sejam confirmadas juntas. A fila possui processamento pelo menos uma vez, retry com backoff, dead-letter, retenção, leasing concorrente e isolamento por Tenant. A implementação atual está descrita em [docs/transactional-outbox.md](docs/transactional-outbox.md) e no [ADR-003](openspec/decisions/ADR-003-transactional-outbox.md).

Essa escolha mantém o desenvolvimento simples e confiável enquanto o produto evolui dentro do monólito. Um broker externo poderá ser adicionado futuramente sem tornar o domínio dependente dele.

### Fluxos de escrita e leitura

```text
Escrita:
HTTP → Controller → CommandDispatcher → Validator → CommandHandler
      → Domain → Write Gateway → EF Core → PostgreSQL

Leitura:
HTTP → Controller → QueryDispatcher → Validator → QueryHandler
      → Read Gateway → Dapper → PostgreSQL → Read Model
```

O domínio permanece independente de HTTP, EF Core, Dapper, PostgreSQL, Docker e Vue. Controllers não acessam banco nem concentram regras de negócio.

## Stack

| Camada | Tecnologias |
| --- | --- |
| Backend | .NET 10, C#, ASP.NET Core |
| Frontend | Vue.js, Quasar, TypeScript |
| Persistência | PostgreSQL, Entity Framework Core, Dapper |
| Execução local | Docker, Docker Compose, Visual Studio |
| Qualidade | Testes unitários, integração e testes de frontend |
| Arquitetura | Modular Monolith, Clean Architecture, DDD, CQRS |
| Mensageria atual | Transactional Outbox, PostgreSQL, Background Worker |
| Observabilidade atual | Logs estruturados e diagnóstico operacional local |

## Executando localmente

### Pré-requisitos

- .NET SDK 10;
- Node.js 22.22 ou superior;
- Docker 29 com Docker Compose;
- Visual Studio ou terminal PowerShell.

### Docker Compose

```powershell
docker compose up --build
```

Serviços padrão:

| Serviço | URL |
| --- | --- |
| Aplicação web | http://localhost:9000 |
| API | http://localhost:8080 |
| Mailpit | http://localhost:8025 |
| SMTP local | localhost:1025 |
| PostgreSQL | localhost:5432 |

O serviço de migrations é executado antes da API. Não é necessário executar `dotnet ef database update` manualmente para o fluxo padrão do Compose.

Para acompanhar o diagnóstico:

```powershell
docker compose ps -a
docker compose logs migrations
docker compose logs api
```

> Se o Visual Studio já estiver executando o projeto, não inicie uma segunda composição usando as mesmas portas. O Visual Studio já cria o stack do Compose, incluindo o Mailpit.

### Backend sem Docker

```powershell
dotnet restore ZFome.sln
dotnet build ZFome.sln --no-restore
dotnet test ZFome.sln --no-build
dotnet run --project src/ZFome.Api
```

### Frontend isolado

```powershell
cd web/ZFome.Web
npm install
npm run dev
```

## Testes e validação

Backend:

```powershell
dotnet test ZFome.sln
```

Frontend:

```powershell
cd web/ZFome.Web
npm run build
npm test
```

Antes de abrir uma mudança, revise também:

- isolamento de tenant;
- validação de entrada com FluentValidation;
- propagação de `CancellationToken`;
- acessibilidade e responsividade;
- contraste nos modos claro e escuro;
- testes arquiteturais e de integração relevantes.

## Estrutura do repositório

```text
src/
├── ZFome.Domain/                 # Modelo, agregados e regras de domínio
├── ZFome.Application/            # Commands, Queries, handlers e portas
├── ZFome.Infrastructure/         # EF Core, Dapper e adapters técnicos
├── ZFome.Infrastructure.Migrations/
├── ZFome.Contracts/              # Contratos externos da API
└── ZFome.Api/                    # Composition root e borda HTTP

web/ZFome.Web/
├── src/components/               # Componentes compartilhados
├── src/layouts/                  # Public, Administration, Operations e Platform
├── src/modules/                  # Features organizadas por domínio
├── src/pages/                    # Páginas de entrada e páginas específicas
├── src/themes/                   # Tokens e preferência de aparência
└── public/brand/                 # Logo e favicon do zFome

openspec/                         # Specs, changes e decisões arquiteturais
docs/                             # Documentação operacional e Design System
```

## Documentação

- [Design System web](docs/web-design-system.md)
- [Notificações de pedidos em tempo real](docs/realtime-order-notifications.md)
- [Transactional Outbox](docs/transactional-outbox.md)
- [Arquitetura](openspec/architecture.md)
- [Convenções](openspec/conventions.md)
- [ADR-003 — Transactional Outbox](openspec/decisions/ADR-003-transactional-outbox.md)
- [Specs e mudanças OpenSpec](openspec/)
- Cérebro do zFome: `C:\Users\marco\source\repos\Obsidian\Arquiteturando\Projetos\zFome\00 - Home\Home.md` (ambiente local)

O Cérebro do zFome mantém contexto de produto, domínio, fluxos, decisões e histórico. Para requisitos vigentes, prevalecem `AGENTS.md`, OpenSpec, ADRs, Design System e o código atual.

## Evolução planejada

O roadmap técnico separa capacidades existentes de integrações futuras. A intenção é evoluir por necessidade comprovada, mantendo o núcleo do zFome independente de fornecedores.

### RabbitMQ — transporte externo futuro

O RabbitMQ poderá ser introduzido quando houver necessidade de consumidores independentes, maior escala de processamento ou integração entre processos. O desenho previsto é:

```text
PostgreSQL Outbox → Relay confiável → RabbitMQ → Consumidores
```

Nesse modelo, a Outbox continua sendo a fonte de confiabilidade transacional e o RabbitMQ atua como transporte distribuído. A implementação exigirá uma spec e um ADR próprios, além de exchanges, filas, routing keys versionadas, publisher confirms, retry, dead-letter e consumidores idempotentes.

O RabbitMQ não faz parte do ambiente padrão atual; o desenvolvimento local poderá utilizá-lo via Docker Compose quando essa capability for aprovada.

### Observability — Datadog futuro

O projeto também prevê uma camada de observabilidade evolutiva, com possibilidade de integração ao Datadog para:

- métricas de API, workers e filas;
- traces distribuídos entre comandos, Outbox e handlers;
- logs estruturados correlacionados por requisição, mensagem e Tenant;
- dashboards de latência, erro, backlog e dead-letter;
- alertas de falhas, mensagens antigas e degradação operacional.

Até essa integração ser implementada, o projeto mantém diagnóstico local por logs, testes e consultas operacionais documentadas. A observabilidade não deve expor payloads sensíveis, tokens ou dados de outro Tenant.

### Produto e plataforma

- consolidar a experiência de captação e onboarding de estabelecimentos;
- evoluir descoberta de estabelecimentos por CEP;
- ampliar notificações e integrações de comunicação;
- amadurecer autenticação opcional do cliente;
- acompanhar métricas operacionais e de conversão;
- manter a evolução incremental do monólito modular.

## Contribuindo

Antes de alterar o projeto:

1. leia o `AGENTS.md` aplicável;
2. consulte arquitetura, convenções, ADRs e a spec relacionada;
3. pesquise implementações e componentes existentes;
4. implemente a menor mudança coerente;
5. execute build, testes e validações relevantes.

Não adicione MediatR, AutoMapper ou regras de negócio em Controllers. Mudanças arquiteturais exigem ADR antes da implementação.

## Licença

Projeto em desenvolvimento. Consulte os responsáveis pelo repositório antes de redistribuir ou utilizar o software fora do contexto autorizado.
