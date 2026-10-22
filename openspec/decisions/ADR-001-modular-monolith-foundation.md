# ADR-001 — Fundação em monólito modular

## Contexto

O zFome precisa iniciar como uma aplicação SaaS Multi-Tenant para estabelecimentos de alimentação, com áreas pública, operacional e administrativa. O produto possui um roadmap amplo, mas ainda não há necessidade comprovada de implantação ou escala independente entre bounded contexts.

## Problema

É necessário definir uma estrutura inicial que preserve isolamento de domínio, CQRS, segurança Multi-Tenant e evolução futura sem multiplicar serviços, projetos e infraestrutura antes de existirem casos de uso concretos.

## Opções

### Opção A — Microservices por bounded context

Permite implantação independente, mas exige rede, observabilidade distribuída, consistência eventual e operação de múltiplos serviços desde a primeira entrega.

### Opção B — Projeto único sem limites internos

É simples para iniciar, mas facilita acoplamento entre domínio, persistência, HTTP e módulos de negócio.

### Opção C — Monólito modular com camadas e módulos explícitos

Mantém um único processo e banco inicialmente, separa Domain, Application, Infrastructure, Contracts e API, e organiza capacidades por bounded context dentro dessas responsabilidades.

## Decisão

Adotar a Opção C. A solution terá cinco projetos de produção:

- `ZFome.Domain`, independente de frameworks externos;
- `ZFome.Application`, com casos de uso, portas e CQRS;
- `ZFome.Infrastructure`, com adapters, EF Core, Dapper e integrações técnicas;
- `ZFome.Contracts`, com contratos externos explícitos;
- `ZFome.Api`, como composition root e borda HTTP.

Os primeiros módulos previstos são Tenancy, Identity, Catalog, Customers e Orders, introduzidos apenas nas fases correspondentes. A comunicação entre módulos ocorrerá por contratos explícitos. EF Core será usado na escrita, Dapper na leitura e PostgreSQL será compartilhado, com ownership por schema quando houver tabelas reais.

Microservices, mensageria, Redis, SQLite e fornecedores externos não fazem parte da fundação. Qualquer adoção futura exige necessidade comprovada, spec e ADR próprios.

## Consequências

### Positivas

- Menor complexidade operacional na primeira versão.
- Dependências podem ser protegidas por testes arquiteturais.
- CQRS e isolamento Multi-Tenant são estabelecidos desde o início.
- Módulos podem ser extraídos futuramente se houver motivação concreta.

### Negativas

- Um único processo e banco limitam implantação independente.
- O banco compartilhado pode permitir acoplamento acidental se ownership e testes forem ignorados.
- A disciplina modular depende de contratos e verificações automatizadas.
