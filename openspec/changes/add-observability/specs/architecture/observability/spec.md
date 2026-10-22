## Purpose

Torna saúde, falhas e desempenho do zFome observáveis sem acoplar o domínio nem expor dados sensíveis entre Tenants.

## ADDED Requirements

### Requirement: Telemetria é correlacionável e segura
Requisições e operações assíncronas SHALL produzir correlação em logs, métricas e traces, e MUST NOT registrar segredos, credenciais ou conteúdo pessoal não autorizado.

#### Scenario: Falha durante confirmação
- **WHEN** uma operação atravessa API, handler e banco
- **THEN** seus sinais podem ser correlacionados sem expor dados de outro Tenant

### Requirement: Saúde distingue processo pronto de processo vivo
O sistema SHALL expor verificações separadas de liveness e readiness, refletindo dependências indispensáveis sem executar mutações.

#### Scenario: Banco indisponível
- **WHEN** o processo responde mas não acessa PostgreSQL
- **THEN** liveness pode permanecer saudável e readiness indica indisponibilidade
