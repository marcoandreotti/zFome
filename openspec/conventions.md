# Convenções do zFome

## Linguagem e plataforma

- Backend em C# sobre .NET 10, com nullable reference types habilitado.
- Frontend em Vue.js, Quasar, Composition API e TypeScript.
- PostgreSQL é o banco principal.
- Entity Framework Core é exclusivo do fluxo de escrita; Dapper é exclusivo do fluxo de leitura.
- MediatR e AutoMapper são proibidos, inclusive como dependências transitivas deliberadamente adicionadas.

## Organização e dependências

- Usar file-scoped namespaces e código moderno quando isso melhorar a clareza.
- Domain não referencia Application, Infrastructure, API, EF Core, Dapper, HTTP ou fornecedores externos.
- Application referencia Domain e define portas necessárias aos casos de uso.
- Infrastructure implementa portas das camadas internas.
- API é o composition root e mantém Controllers finos.
- Contracts contém contratos externos explícitos e não expõe entidades de domínio.
- SQL permanece em Infrastructure, junto aos adapters de leitura.

## Frontend e Design System

- `docs/web-design-system.md` é a fonte oficial das regras visuais, superfícies e catálogo de componentes web.
- Antes de criar páginas ou componentes, pesquisar tokens, layouts, componentes compartilhados e componentes do módulo.
- Classificar componentes novos como globais, de domínio/feature ou específicos de página; não promover abstrações prematuramente.
- Usar componentes Quasar diretamente quando forem estrutura local sem padrão de aplicação correspondente; wrappers globais precisam acrescentar semântica, comportamento ou consistência real.
- Preservar as identidades distintas de Public, Administration, Operations, KDS e Platform.
- Evitar valores visuais arbitrários quando existir token semântico aplicável.
- Toda alteração frontend considera os estados relevantes, responsividade, teclado, foco, semântica, contraste e touch.

## CQRS e validação

- Commands alteram estado; Queries nunca alteram estado.
- Usar dispatchers próprios: `ICommandDispatcher` e `IQueryDispatcher`.
- Validar entradas com FluentValidation antes de executar handlers.
- Regras de negócio e invariantes ficam no Domain, não em validators, Controllers ou repositories.
- Toda operação de I/O é assíncrona e propaga `CancellationToken`; `.Result` e `.Wait()` são proibidos.

## Persistência e Multi-Tenancy

- Repositories e gateways representam operações relevantes ao domínio; não duplicar `DbSet` por meio de abstrações genéricas.
- Abstrações genéricas só são criadas quando ao menos dois casos reais compartilham o mesmo comportamento.
- Dados de estabelecimento são tenant-scoped e carregam `TenantId` quando aplicável.
- Autorização nunca confia em `TenantId` recebido do cliente.
- Consultas EF Core e Dapper devem garantir isolamento entre Tenants e possuir testes quando houver persistência real.

## API e erros

- Controllers recebem contratos, despacham Commands ou Queries e produzem respostas HTTP.
- Controllers não acessam EF Core, Dapper ou regras de negócio.
- Erros conhecidos são tratados centralmente e retornados como ProblemDetails.
- Mapeamentos são explícitos por construtores, factories ou métodos `From` legíveis.

## Testes e conclusão

- Testar invariantes no Domain, handlers e validators na Application e I/O por testes de integração.
- Manter testes arquiteturais para as dependências proibidas.
- Uma tarefa só termina com build sem erros, testes relevantes passando e sem warnings relevantes.
- Não considerar comportamento especificado como concluído se foi apenas parcialmente implementado ou adiado.

## Evolução arquitetural

- Começar como monólito modular; microservices e mensageria exigem spec e ADR próprios.
- Toda mudança de decisão arquitetural exige ADR em `openspec/decisions/` antes da implementação.
- Redis, SQLite, PWA, IA e integrações externas só são introduzidos quando uma spec demonstrar necessidade real.
