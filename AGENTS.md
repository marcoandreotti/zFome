# AGENTS.md

# zFome — Codex Development Rules

Este arquivo contém regras permanentes e obrigatórias para qualquer agente que altere este repositório.

Antes de criar, alterar, refatorar ou remover código:

1. Leia completamente o `AGENTS.md` aplicável ao escopo.
2. Leia `openspec/project.md`, `openspec/architecture.md` e `openspec/conventions.md`.
3. Consulte os ADRs relevantes em `openspec/decisions/`.
4. Localize e leia a spec correspondente em `openspec/specs/` e os artefatos da change ativa, quando houver.
5. Pesquise implementações, abstrações, componentes e testes equivalentes antes de criar algo novo.
6. Para qualquer alteração frontend, leia `docs/web-design-system.md` e consulte os tokens, layouts e componentes existentes.
7. Não implemente requisitos que não estejam na spec atual.
8. Não altere decisões arquiteturais sem registrar um ADR.

Regra permanente:

> Antes de escrever código, descubra como o zFome já resolve o problema.
> Reutilize padrões existentes quando forem semanticamente adequados.
> Não crie uma segunda maneira de resolver um problema que já possui solução oficial.
> Se o padrão existente não atender à necessidade, não o contorne silenciosamente: identifique a lacuna e determine se ela exige extensão, novo padrão, atualização de spec ou decisão arquitetural.

## Hierarquia de autoridade

Quando fontes divergirem, utilize esta ordem:

1. `AGENTS.md` aplicável ao escopo;
2. arquitetura e ADRs aprovados em `openspec/architecture.md` e `openspec/decisions/`;
3. Design System e padrões oficiais de componentes em `docs/web-design-system.md`;
4. convenções globais em `openspec/conventions.md`;
5. OpenSpec da funcionalidade atual;
6. implementação existente que seja coerente com as fontes anteriores.

Uma spec de funcionalidade não pode violar silenciosamente uma decisão arquitetural global. Se houver conflito entre fontes, não escolha arbitrariamente e não implemente um desvio: registre o conflito, os arquivos envolvidos e a decisão necessária.

---

# 1. Arquitetura

A solução utiliza:

- .NET 10
- C#
- PostgreSQL
- Entity Framework Core para escrita
- Dapper para leitura
- Vue.js
- Quasar
- TypeScript
- Docker

Princípios arquiteturais:

- Hexagonal Architecture
- Clean Architecture
- DDD
- CQRS
- SOLID
- Clean Code
- Modular Monolith

---

# 2. Proibições

É proibido utilizar:

- MediatR
- AutoMapper

Não adicionar nenhuma dessas bibliotecas direta ou indiretamente.

Também é proibido:

- colocar regra de negócio em Controllers;
- acessar DbContext diretamente de Controllers;
- acessar Dapper diretamente de Controllers;
- retornar entidades de domínio pela API;
- colocar SQL no projeto Domain;
- referenciar EF Core no Domain;
- referenciar Dapper no Domain;
- referenciar Infrastructure no Domain;
- colocar regra de domínio em Repository;
- utilizar Service Locator;
- utilizar dependências estáticas para resolver serviços;
- criar abstrações sem necessidade real;
- criar microservices;
- introduzir mensageria sem uma spec específica.

---

# 3. Regra fundamental

Controllers devem ser finos.

Fluxo de escrita:

HTTP
→ Controller
→ CommandDispatcher
→ Validator
→ CommandHandler
→ Domain
→ Write Gateway / Repository
→ EF Core
→ PostgreSQL

Fluxo de leitura:

HTTP
→ Controller
→ QueryDispatcher
→ Validator quando necessário
→ QueryHandler
→ Read Gateway
→ Dapper
→ PostgreSQL
→ Read Model

Nunca misturar os dois fluxos.

---

# 4. CQRS

Commands alteram estado.

Queries nunca alteram estado.

Commands não devem existir para simplesmente consultar informações.

Queries não devem executar INSERT, UPDATE ou DELETE.

Utilizar:

- ICommand
- ICommand<TResult>
- ICommandHandler<TCommand>
- ICommandHandler<TCommand, TResult>
- ICommandDispatcher
- IQuery<TResult>
- IQueryHandler<TQuery, TResult>
- IQueryDispatcher

Os Dispatchers são implementados pelo próprio projeto.

Não utilizar MediatR.

---

# 5. Validation

Utilizar FluentValidation.

Validações de entrada devem ficar nos Validators.

Exemplos:

- campo obrigatório;
- tamanho;
- formato;
- range;
- formato de e-mail;
- IDs inválidos.

Regras de negócio pertencem ao domínio.

Exemplo:

ERRADO:

OrderCommandValidator:
    pedido não pode ser cancelado porque já saiu para entrega.

CORRETO:

Order.Cancel()

A entidade/agregado deve proteger essa regra.

---

# 6. Domain

O Domain deve permanecer independente.

Pode conter:

- Aggregates
- Entities
- Value Objects
- Domain Services
- Domain Events
- Domain Exceptions
- Specifications quando necessário

O Domain não pode conhecer:

- EF Core
- Dapper
- PostgreSQL
- HTTP
- Controllers
- Swagger
- Redis
- Docker
- Vue
- Quasar
- serviços externos

---

# 7. Persistência de escrita

Entity Framework Core é utilizado para escrita.

Repositories devem representar operações relevantes ao domínio.

Evitar interfaces genéricas como:

IRepository<TEntity>
{
    Add
    Update
    Delete
    GetAll
}

quando forem apenas uma cópia do DbSet.

Preferir:

IOrderRepository
{
    GetForUpdateAsync(...)
    AddAsync(...)
}

Abstrações genéricas são permitidas somente quando representarem comportamento realmente comum.

---

# 8. Persistência de leitura

Dapper é utilizado para leitura.

Queries podem:

- usar joins;
- utilizar CTE;
- fazer projections;
- retornar DTOs específicos;
- utilizar agregações;
- aplicar filtros;
- aplicar paginação.

Não carregar Aggregate Roots apenas para montar consultas.

---

# 9. Multi-Tenant

Toda funcionalidade pertencente a um estabelecimento deve considerar TenantId.

Nunca confiar em TenantId enviado pelo cliente para autorização.

O Tenant deve ser obtido de contexto autenticado ou mecanismo equivalente.

Toda consulta deve garantir isolamento entre Tenants.

Nunca permitir acesso cruzado de dados.

---

# 10. API

Controllers:

- recebem requests;
- executam Dispatcher;
- retornam resultado HTTP.

Controllers NÃO:

- executam regra de negócio;
- acessam banco;
- criam queries SQL;
- executam EF diretamente;
- fazem mapeamentos complexos.

---

# 11. Exceptions

Não espalhar try/catch pelos Controllers.

Exceptions são tratadas pelo middleware global.

Utilizar ProblemDetails.

Tipos esperados:

- ValidationException
- DomainException
- NotFoundException
- ConflictException
- ForbiddenException
- UnauthorizedException

---

# 12. Mapping

AutoMapper é proibido.

Mapeamentos simples devem ser explícitos.

Preferir:

ProductResponse.From(product)

ou

new ProductResponse(...)

Mapeamentos devem permanecer legíveis.

---

# 13. Generics

Antes de criar uma abstração genérica, pergunte:

"Existem pelo menos dois casos reais com exatamente o mesmo comportamento?"

Se a resposta for não, não criar generic prematuramente.

Não criar abstrações especulativas.

---

# 14. Async

Toda operação de I/O deve utilizar async/await.

Propagar CancellationToken.

Exemplo:

Task<Order?> GetAsync(
    Guid id,
    CancellationToken cancellationToken);

Não utilizar:

.Result
.Wait()

---

# 15. Código moderno

Utilizar recursos modernos do C# quando aumentarem clareza.

Preferir:

- records para contracts imutáveis;
- nullable reference types;
- required quando adequado;
- primary constructors quando melhorarem legibilidade;
- pattern matching;
- file-scoped namespaces.

Não utilizar recursos modernos apenas por estética.

---

# 16. Testes

Para cada regra relevante adicionar testes.

Domain:
- regras;
- invariantes;
- transições de estado.

Application:
- handlers;
- validators.

Infrastructure:
- integration tests quando necessário.

API:
- integration tests para fluxos importantes.

Não escrever testes que apenas reproduzam a implementação.

---

# 17. Feature Workflow

Ao implementar uma feature:

1. localizar a spec;
2. identificar bounded context;
3. identificar Command ou Query;
4. criar contrato;
5. criar Validator;
6. criar Handler;
7. criar abstrações necessárias;
8. implementar Adapter;
9. criar endpoint;
10. criar testes;
11. executar testes;
12. executar build;
13. atualizar documentação quando necessário.

---

# 18. Antes de criar código novo

Pesquise primeiro no projeto.

Nunca criar uma classe, interface, helper ou abstração sem verificar se já existe equivalente.

Reutilizar código existente quando semanticamente apropriado.

Não criar duplicações com nomes diferentes.

Antes de criar abstrações como `IRepository<T>`, `IService<T>`, `IHandler<T>`, factories, helpers ou infraestrutura genérica, verificar:

1. se já existe padrão equivalente;
2. se existe necessidade concreta;
3. se há pelo menos dois casos reais com o mesmo comportamento quando houver generalização;
4. se a abstração respeita as dependências arquiteturais;
5. se composição ou extensão localizada resolve melhor o problema.

---

# 19. Mudanças arquiteturais

Se uma tarefa exigir mudança arquitetural:

NÃO alterar silenciosamente.

Criar primeiro:

openspec/decisions/ADR-XXX-<descricao>.md

Explicar:

- Contexto
- Problema
- Opções
- Decisão
- Consequências

Somente então implementar.

---

# 20. Definition of Done

Uma feature só está pronta quando:

- [ ] Compila sem erros
- [ ] Testes existentes continuam passando
- [ ] Novos testes foram adicionados quando necessários
- [ ] Não viola dependências arquiteturais
- [ ] Não introduz warnings relevantes
- [ ] CancellationToken foi propagado
- [ ] Validação foi implementada
- [ ] Tenant isolation foi considerada
- [ ] Tratamento de erro está padronizado
- [ ] API não retorna entidades
- [ ] Código duplicado relevante não foi introduzido
- [ ] Spec foi atendida completamente

"Funciona" não significa "pronto".

---

# 21. Frontend e Design System

`docs/web-design-system.md` é a fonte oficial das regras visuais, superfícies, tokens, classificação e catálogo de componentes do frontend.

Antes de criar ou modificar página, layout ou componente Vue/Quasar:

1. consultar o Design System;
2. pesquisar componentes compartilhados e componentes do módulo;
3. consultar `src/themes/`, `src/css/app.scss` e os layouts existentes;
4. verificar padrões de formulário, navegação, feedback e estados;
5. classificar qualquer novo componente como global, de domínio/feature ou específico de página;
6. analisar smartphone, tablet e desktop nos tamanhos aplicáveis;
7. considerar loading, success, empty, error, disabled e unauthorized quando aplicáveis;
8. verificar teclado, foco, rótulos, semântica, contraste e áreas de toque.

Não iniciar uma tela usando componentes Quasar sem verificar se existe padrão de aplicação correspondente. Um wrapper compartilhado deve ser usado quando representar comportamento ou linguagem oficial. Componentes Quasar podem ser usados diretamente para estruturas locais quando não houver padrão global.

Não criar wrappers sem benefício real e não transformar componentes compartilhados em componentes excessivamente configuráveis apenas para evitar especialização legítima.

---

# 22. Reutilização de componentes e tokens

Antes de criar um componente, pesquisar por responsabilidade, comportamento, aparência, nome e contexto de uso.

Classificação obrigatória:

- **Global:** padrão estável utilizado por múltiplos contextos; pertence ao Design System ou a `src/components/`.
- **Domain/Feature:** reutilizável dentro de um módulo; permanece próximo ao módulo correspondente.
- **Page-specific:** existe para uma única página; permanece próximo à página.

Quando existir componente semelhante:

1. avaliar reutilização direta;
2. avaliar composição, props ou slots coerentes;
3. avaliar extensão do padrão existente;
4. somente então criar um componente diferente.

Não duplicar conceitos por variações de nome. Também não promover automaticamente componentes de feature para o Design System.

É proibido espalhar valores visuais arbitrários quando existir token correspondente. Consultar tokens antes de adicionar cores, espaçamentos, raios, sombras, tipografia, breakpoints ou z-index. Um novo token deve representar uma decisão semântica e reutilizável, não apenas substituir um valor isolado.

---

# 23. Superfícies, responsividade e acessibilidade

As superfícies possuem propósitos diferentes e não devem compartilhar um layout apenas por conveniência técnica:

- **Public:** mobile-first, identidade do Tenant, fotografia, descoberta, conversão, carrinho e composição de produtos.
- **Administration:** produtividade, clareza, densidade controlada, formulários, tabelas, indicadores e navegação consistente. A marca do Tenant não pode comprometer a consistência administrativa.
- **Operations:** velocidade, alto contraste, estados e tempo claros, poucos cliques, atualização em tempo real e uso em monitor, tablet ou touch.
- **KDS:** leitura à distância, prioridade, tempo prometido, modificadores e observações de produção.
- **Platform:** contexto global institucional, separado visual e operacionalmente dos dados de um Tenant.

Os layouts oficiais atuais são `PublicLayout`, `AdministrationLayout`, `OperationsLayout` e `PlatformLayout`.

Toda alteração frontend deve preservar:

- navegação por teclado e foco visível;
- rótulos e semântica HTML;
- contraste adequado;
- estado comunicado também por texto ou ícone, não somente por cor;
- áreas acionáveis adequadas para touch;
- responsividade sem lógica JavaScript de layout quando CSS ou Quasar forem suficientes.

---

# 24. Alteração de padrões

Uma feature não autoriza refatoração arquitetural ou visual global.

Ao identificar uma solução melhor que o padrão consolidado:

1. documentar o problema e o padrão atual;
2. apresentar a alternativa e o impacto;
3. listar os arquivos e consumidores afetados;
4. determinar se a mudança exige atualização do Design System, da spec ou um ADR;
5. obter a decisão necessária antes da substituição global.

Um novo componente global deve ter propósito e limites documentados, usar tokens oficiais, registrar props, slots e eventos relevantes e possuir testes quando tiver comportamento próprio. Atualizar o catálogo em `docs/web-design-system.md` na mesma mudança.

OpenSpecs descrevem principalmente o que muda em uma capability. Não redefinir regras permanentes dentro de uma feature; referenciar arquitetura, convenções e Design System. Qualquer necessidade de alterar esses padrões deve ser declarada explicitamente.

---

# 25. Checklist antes de implementar

## Architecture Check

- [ ] Li o `AGENTS.md` aplicável.
- [ ] Consultei projeto, arquitetura, convenções e ADRs relevantes.
- [ ] Consultei a OpenSpec e a change da feature.
- [ ] Pesquisei implementação e testes equivalentes.
- [ ] Não estou duplicando abstrações existentes.
- [ ] A mudança respeita CQRS, DDD e arquitetura hexagonal quando aplicável.
- [ ] Considerei Multi-Tenancy, segurança, autenticação e autorização.

## Frontend Check

- [ ] Consultei `docs/web-design-system.md`.
- [ ] Consultei componentes, layouts e tokens existentes.
- [ ] Classifiquei novos componentes como Global, Domain/Feature ou Page-specific.
- [ ] Não dupliquei componente ou padrão existente.
- [ ] Respeitei a identidade da superfície afetada.
- [ ] Considerei smartphone, tablet e desktop aplicáveis.
- [ ] Considerei loading, success, empty, error, disabled e unauthorized aplicáveis.
- [ ] Considerei teclado, foco, semântica, contraste e touch.

---

# 26. Checklist após implementar

Antes de declarar uma tarefa concluída:

- executar build e testes relevantes;
- executar testes arquiteturais;
- executar lint, formatação e typecheck aplicáveis;
- revisar warnings e arquivos alterados;
- verificar violações de arquitetura e isolamento Multi-Tenant;
- verificar duplicação de abstrações e componentes;
- verificar valores visuais hardcoded quando houver token aplicável;
- verificar responsividade e acessibilidade nas superfícies afetadas;
- atualizar documentação e catálogo quando um padrão for criado ou alterado.

"Compilou" não significa "está aderente à arquitetura".

"Ficou bonito" não significa "está aderente ao Design System".

---

# 27. Cérebro do projeto e memória

O cofre Obsidian é o **Cérebro do zFome**: a base de conhecimento do projeto para produto, domínio, arquitetura, decisões, fluxos, padrões e orientações para agentes. No início de cada tarefa, consulte o Cérebro e a memória disponível quando puderem esclarecer o contexto, a intenção, o histórico ou os termos do pedido. Navegue pelas notas relacionadas à tarefa; não é necessário percorrer notas sem relação com ela.

O cofre do zFome fica no repositório `C:\Users\marco\source\repos\Obsidian`, em `Arquiteturando/Projetos/zFome/`. Comece por `00 - Home/Home.md` ou `00 - Home/Mapa do Projeto.md` para navegar e siga os links para a área relacionada. A nota `Arquiteturando/Bússola de desenvolvimento para agentes.md` e a seção `09 - Agentes/` contêm orientações complementares.

Use o Cérebro para formar o entendimento do projeto e navegar pelo conhecimento registrado. Para requisitos vigentes e decisões normativas, confirme nas fontes oficiais do zFome: `AGENTS.md`, OpenSpec, ADRs e Design System, conforme a hierarquia deste arquivo. Em caso de divergência, prevalecem essas fontes oficiais. Diferencie decisões documentadas, observações do código, inferências e informações a confirmar; verifique informações sujeitas a mudança no repositório atual e não transforme inferências em requisitos.

Se o cofre não estiver disponível, prossiga com as fontes oficiais acessíveis e registre a limitação somente quando ela afetar a tarefa.
