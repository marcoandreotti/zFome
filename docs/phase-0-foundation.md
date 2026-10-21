# Fase 0 — Fundação arquitetural

## Estado

A Fase 0 estabelece uma aplicação executável e testável, sem implementar funcionalidades de catálogo, pedidos, identidade ou administração.

## Estrutura implementada

- Solution .NET 10 com Domain, Application, Infrastructure, Contracts e API.
- Testes separados em Domain, Application, Integration e Architecture.
- CQRS próprio com Commands, Queries, handlers e dispatchers resolvidos pelo DI nativo.
- FluentValidation executado antes dos handlers.
- Middleware global de ProblemDetails e correlation ID.
- Contexto de Tenant baseado no claim autenticado `tenant_id`, falhando de modo seguro quando ausente.
- EF Core/Npgsql para escrita e Dapper/Npgsql para leitura.
- Convenções de schemas e entidades tenant-scoped, sem tabelas de negócio antecipadas.
- Shell Vue/Quasar/TypeScript com layouts público, operacional e administrativo.
- Cliente HTTP com correlation ID e tratamento de ProblemDetails.
- Tokens de tema e tema padrão centralizados.
- Docker Compose com PostgreSQL, API, Web, rede, volume e health checks.

## Endpoints da fundação

- `GET /health`: liveness do processo da API.
- `GET /health/ready`: readiness com conectividade PostgreSQL.

A rota `/_test/tenant` existe somente no ambiente `Testing` para comprovar a falha segura do contexto de Tenant e não é exposta nos ambientes normais.

## Verificações executadas

- `dotnet build ZFome.sln --no-restore`: sucesso, 0 erros e 0 warnings.
- Testes Application: 5 aprovados.
- Testes Architecture: 6 aprovados.
- Testes Integration/PostgreSQL: 4 aprovados.
- Quasar/Vue typecheck: sucesso.
- Quasar production build: sucesso.
- Docker Compose: PostgreSQL, API e Web saudáveis.
- API readiness: HTTP 200, corpo `Healthy`.
- Frontend: HTTP 200.

O projeto Domain ainda não possui regras concretas nesta fase; por isso seu projeto de testes está preparado, mas não contém testes artificiais.

## Decisões e limites

O ADR-001 foi implementado sem divergências. A fundação permanece como monólito modular e não introduz MediatR, AutoMapper, mensageria, Redis, SQLite, PWA, IA ou integrações externas.

A próxima mudança deve possuir spec própria e iniciar a Fase 1, Tenancy + Identity, sem expandir silenciosamente o escopo desta fundação.
