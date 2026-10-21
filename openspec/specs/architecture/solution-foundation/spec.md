# Solution Foundation Specification

## Purpose

Estabelece os requisitos verificáveis da fundação técnica do zFome para que cada fase preserve isolamento Multi-Tenant, separação CQRS, independência do domínio e capacidade de execução e teste incremental.

## Requirements

### Requirement: Monólito modular com dependências controladas
O sistema SHALL ser entregue inicialmente como um monólito modular, com limites explícitos entre módulos de negócio e sem dependência do Domain em infraestrutura, transporte HTTP, persistência ou fornecedores externos.

#### Scenario: Validação das dependências arquiteturais
- **WHEN** os testes arquiteturais analisarem as referências entre projetos e módulos
- **THEN** referências que façam o Domain depender de Infrastructure, API, EF Core, Dapper ou serviços externos SHALL ser rejeitadas

#### Scenario: Comunicação entre módulos
- **WHEN** uma capacidade precisar colaborar com outro módulo
- **THEN** a colaboração SHALL ocorrer por contratos explícitos e não por acesso direto às estruturas internas do outro módulo

### Requirement: Separação entre escrita e leitura
O sistema SHALL separar operações que alteram estado das operações de consulta, sem usar MediatR e sem compartilhar handlers ou modelos de persistência entre os dois fluxos.

#### Scenario: Execução de comando
- **WHEN** uma requisição válida solicitar alteração de estado
- **THEN** ela SHALL seguir Controller, CommandDispatcher, validação, CommandHandler, domínio e porta de escrita antes da persistência

#### Scenario: Execução de consulta
- **WHEN** uma requisição solicitar apenas dados
- **THEN** ela SHALL seguir Controller, QueryDispatcher, validação quando necessária, QueryHandler e porta de leitura, retornando um modelo de leitura sem alterar estado

#### Scenario: Entrada inválida
- **WHEN** uma entrada violar regras de formato, obrigatoriedade, tamanho ou faixa
- **THEN** a execução do handler SHALL ser impedida e a API SHALL retornar um ProblemDetails de validação padronizado

### Requirement: Regras de negócio protegidas pelo domínio
O sistema MUST manter invariantes, transições de estado e demais regras de negócio no modelo de domínio, mantendo Controllers e adaptadores de persistência livres dessas decisões.

#### Scenario: Transição inválida de pedido
- **WHEN** for solicitada uma transição de pedido não permitida pelo estado atual
- **THEN** o agregado SHALL rejeitar a operação com um erro de domínio padronizável pela API

#### Scenario: Contrato HTTP
- **WHEN** a API concluir uma operação
- **THEN** a resposta SHALL usar um contrato explícito e MUST NOT expor diretamente uma entidade de domínio

### Requirement: Isolamento Multi-Tenant
O sistema MUST representar Tenant como grupo proprietário de uma ou mais unidades operacionais. Toda informação operacional MUST ser associada ao Tenant autenticado e à unidade aplicável, e todas as operações MUST impedir leitura ou alteração cruzada entre Tenants e entre unidades sem associação ativa do usuário no mesmo Tenant.

#### Scenario: Tenant informado pelo cliente
- **WHEN** o cliente enviar um TenantId diferente daquele resolvido pelo contexto autenticado
- **THEN** o sistema MUST ignorar esse valor para autorização e MUST impedir acesso aos dados de outro Tenant

#### Scenario: Unidade informada pelo cliente
- **WHEN** o cliente enviar uma unidade que não pertença ao Tenant resolvido ou à qual o ator não tenha acesso
- **THEN** o sistema MUST negar a operação sem revelar dados da unidade

#### Scenario: Seleção autenticada da unidade
- **WHEN** a rota ou claim indicar uma unidade e o usuário não possuir associação ativa correspondente no Tenant autenticado
- **THEN** o sistema MUST negar a criação do contexto operacional independentemente do identificador enviado no payload

#### Scenario: Consulta de dados do estabelecimento
- **WHEN** uma consulta for executada em contexto de Tenant e unidade
- **THEN** apenas dados pertencentes a ambos os escopos autorizados SHALL compor o resultado
### Requirement: Segurança e erros HTTP padronizados
A área operacional e administrativa MUST exigir autenticação e autorização por políticas, enquanto erros esperados SHALL ser convertidos centralmente em ProblemDetails com códigos HTTP consistentes.

#### Scenario: Operação sem permissão
- **WHEN** um usuário autenticado não satisfizer a política exigida
- **THEN** a API SHALL negar a operação sem revelar dados protegidos

#### Scenario: Erro conhecido
- **WHEN** ocorrer validação, conflito, recurso inexistente, violação de domínio, acesso proibido ou ausência de autenticação
- **THEN** o middleware global SHALL produzir ProblemDetails adequado sem exigir try/catch no Controller

### Requirement: Operações críticas idempotentes e auditáveis
A criação de pedidos e pagamentos MUST admitir proteção contra repetição, e operações relevantes SHALL produzir registros de auditoria correlacionáveis ao Tenant, usuário e requisição.

#### Scenario: Repetição da mesma operação crítica
- **WHEN** a mesma chave de idempotência e o mesmo escopo forem reapresentados
- **THEN** o sistema SHALL impedir a duplicação do efeito de negócio e SHALL retornar um resultado consistente com a primeira execução

#### Scenario: Alteração auditável
- **WHEN** uma operação configurada como auditável for concluída
- **THEN** o sistema SHALL registrar Tenant, ator, instante, operação, tipo e identificador do recurso e as mudanças aplicáveis

### Requirement: Experiência web modular e tematizável
A interface Vue/Quasar SHALL ser mobile first, organizada por módulos e SHALL aplicar identidade visual do Tenant por meio de um tema central, sem estilos de marca dispersos pelos componentes.

#### Scenario: Acesso público móvel
- **WHEN** um cliente acessar o cardápio em smartphone por URL ou QR Code
- **THEN** a interface SHALL permanecer utilizável e SHALL aplicar a identidade visual do estabelecimento

#### Scenario: Estrutura hierárquica
- **WHEN** categorias ou adicionais possuírem níveis aninhados
- **THEN** a interface SHALL representá-los sem impor um limite artificial de profundidade na apresentação

### Requirement: Execução local reproduzível
Cada incremento SHALL poder ser compilado, testado e executado de forma reproduzível, com API, aplicação web e PostgreSQL configuráveis por ambiente e sem segredos reais versionados.

#### Scenario: Inicialização do ambiente
- **WHEN** um desenvolvedor iniciar a composição Docker com configuração válida
- **THEN** PostgreSQL, API e aplicação web SHALL iniciar com health checks observáveis e persistência de dados do banco em volume

#### Scenario: Verificação de incremento
- **WHEN** uma fase do roadmap for concluída
- **THEN** build, testes relevantes e validações arquiteturais SHALL passar antes que a fase seja considerada pronta

### Requirement: Documentação OpenAPI no desenvolvimento local
A API SHALL publicar uma descrição OpenAPI e uma interface Swagger UI durante a execução em ambiente Development, sem expor essas interfaces automaticamente nos demais ambientes.

#### Scenario: Consulta da documentação em Development
- **WHEN** a API iniciar com o ambiente Development
- **THEN** o documento OpenAPI SHALL estar acessível por HTTP e a Swagger UI SHALL permitir explorar os endpoints documentados

#### Scenario: Execução fora de Development
- **WHEN** a API iniciar em um ambiente diferente de Development
- **THEN** o documento OpenAPI e a Swagger UI MUST NOT ser publicados

### Requirement: Abertura da aplicação web pelo projeto Docker Compose
O projeto Docker Compose do Visual Studio SHALL iniciar todos os serviços locais, anexar o debugger à API no perfil Debug e abrir a aplicação web no navegador, mantendo a documentação da API disponível em uma URL conhecida.

#### Scenario: Inicialização pelo Visual Studio
- **WHEN** um desenvolvedor iniciar o projeto `docker-compose` pelo perfil Debug
- **THEN** PostgreSQL, API e aplicação web SHALL iniciar
- **AND** o debugger SHALL ser anexado ao processo da API executando os binários Debug atuais
- **AND** o navegador SHALL abrir a aplicação web em `http://localhost:9000`
- **AND** a Swagger UI SHALL estar disponível em `http://localhost:8080/swagger` para acesso manual

#### Scenario: Alteração da API durante o desenvolvimento
- **WHEN** o código da API for alterado e o perfil Debug do projeto Compose for reiniciado
- **THEN** o container da API SHALL executar a versão atualizada dos binários Debug sem depender de uma imagem Release publicada anteriormente

#### Scenario: Inicialização pela CLI
- **WHEN** um desenvolvedor executar `docker compose up --build` pela linha de comando
- **THEN** os serviços SHALL iniciar com imagens atualizadas sem depender do Visual Studio ou da abertura de um navegador

### Requirement: Evolução incremental sem infraestrutura antecipada
O roadmap MUST entregar primeiro a fundação, Tenancy/Identity, catálogo e pedidos, mantendo Redis, mensageria, IA, pagamentos externos, notificações em tempo real e outras integrações opcionais fora da infraestrutura inicial até existir requisito específico.

#### Scenario: Nova integração externa
- **WHEN** uma fase futura exigir um fornecedor externo
- **THEN** uma especificação própria SHALL definir o comportamento e a integração SHALL ser realizada por uma porta controlada pela aplicação

#### Scenario: Avaliação de extração de módulo
- **WHEN** surgir proposta de microservice
- **THEN** a extração SHALL depender de necessidade técnica ou operacional comprovada e de decisão arquitetural registrada

### Requirement: Migrations isoladas das camadas internas
As migrations PostgreSQL SHALL residir em projeto dedicado da camada Infrastructure, usado para design time e deployment, e Domain e Application MUST NOT referenciar esse projeto nem bibliotecas de persistência.

#### Scenario: Validação de referências
- **WHEN** testes arquiteturais analisarem o projeto de migrations
- **THEN** referências das camadas Domain ou Application ao projeto de migrations SHALL ser rejeitadas

### Requirement: Aplicação de migrations antes da API no Docker Compose
O ambiente local Docker Compose SHALL aplicar todas as migrations PostgreSQL pendentes antes de a API começar a atender requisições e SHALL reportar uma falha de migration como dependência de inicialização malsucedida.

#### Scenario: Banco novo ou com migrations pendentes
- **WHEN** um desenvolvedor iniciar o ambiente Docker Compose com o serviço PostgreSQL saudável
- **THEN** o processo de migrations SHALL concluir com sucesso antes de a API iniciar
- **AND** a API SHALL iniciar usando o schema migrado

#### Scenario: Schema do banco já atualizado
- **WHEN** um desenvolvedor iniciar o ambiente sem migrations pendentes
- **THEN** a etapa de migrations SHALL concluir com sucesso sem alterar o schema
- **AND** a API SHALL iniciar normalmente

#### Scenario: Falha ao aplicar migration
- **WHEN** o processo de migrations não conseguir aplicar uma migration pendente
- **THEN** o serviço de migrations SHALL encerrar sem sucesso
- **AND** a API SHALL não iniciar
- **AND** a falha SHALL estar visível na saída dos serviços do Compose
