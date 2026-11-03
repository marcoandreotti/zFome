# Tasks

## 1. Inventário e identidade .NET

- [x] 1.1 Registrar a busca case-insensitive inicial por `OrderHub`, separar artefatos ativos de registros históricos e verificar que a lista cobre código, nomes de arquivos, OpenSpec e Cérebro do projeto.
- [x] 1.2 Renomear com operações rastreáveis pelo Git a solution, projetos, diretórios de produção e testes para `ZFome`, atualizar project references e verificar a estrutura com `dotnet sln ZFome.sln list` e `git status`.
- [x] 1.3 Atualizar namespaces, assembly names, tipos qualificados, imports e referências textuais do backend para `ZFome`, preservando regras e contratos, e verificar com `dotnet build ZFome.sln`.
- [x] 1.4 Renomear o projeto, contexto, factory, snapshot e namespaces de migrations sem alterar IDs cronológicos nem operações de schema, e verificar que o projeto de migrations compila e lista a mesma sequência de migrations.
- [x] 1.5 Atualizar testes e assertions arquiteturais para os assemblies `ZFome.*`, adicionar verificação de que os assemblies esperados foram carregados e executar os testes Domain, Application e Architecture.

## 2. Frontend e identificadores internos

- [x] 2.1 Renomear `web/OrderHub.Web` para `web/ZFome.Web`, atualizar caminhos de build/Docker e o pacote para `zfome-web`, e verificar que instalação e resolução do workspace frontend usam somente o novo caminho.
- [x] 2.2 Atualizar o título e os textos próprios da plataforma para `zFome`, preservando a identidade visual dos Tenants, e ajustar testes de interface para verificar a nova marca nas superfícies aplicáveis.
- [x] 2.3 Alterar a preferência local para uma chave `zfome.*` sem fallback da chave antiga e verificar por teste que um navegador com apenas `orderhub.appearance-mode` inicia na preferência padrão.
- [x] 2.4 Renomear esquema de autenticação, métricas, título OpenAPI, remetentes de exemplo e demais identificadores internos próprios da aplicação, e verificar por testes existentes e busca direcionada que nenhum consumidor ativo depende dos valores anteriores.
- [ ] 2.5 Executar `npm test`, `npm run typecheck` e `npm run build` no frontend renomeado e corrigir qualquer caminho, import ou expectativa residual.

## 3. Ambiente local descartável

- [x] 3.1 Atualizar Compose, Dockerfiles, `.env.example` e appsettings para nomes `zfome` de projeto, banco, usuário, volume, rede e imagens, e verificar a configuração resolvida com `docker compose config`.
- [x] 3.2 Remover deliberadamente o ambiente local anterior com `docker compose down -v --remove-orphans`, confirmar que não há dado a preservar e iniciar os recursos `zfome` a partir de um estado vazio.
- [x] 3.3 Aplicar toda a cadeia existente de migrations no PostgreSQL vazio e verificar pelos logs e pela tabela de histórico que todas as migrations concluíram antes da API iniciar.
- [x] 3.4 Executar smoke test do Compose para health checks da API, abertura da aplicação web e disponibilidade manual do Swagger, registrando qualquer serviço que não alcance o estado saudável.

## 4. Fontes vigentes e Cérebro do projeto

- [x] 4.1 Atualizar `AGENTS.md`, README, PRODUCT, projeto, arquitetura, convenções, Design System, documentação operacional e textos vigentes das specs para `zFome`/`ZFome`/`zfome`, e verificar que todos os comandos e caminhos documentados existem.
- [x] 4.2 Preservar ADRs e changes arquivadas quando forem registros históricos, corrigir apenas referências operacionais que ainda orientem o estado atual e verificar cada ocorrência remanescente contra a allowlist justificada no design.
- [x] 4.3 Renomear a pasta do Cérebro para `Arquiteturando/Projetos/zFome`, atualizar Home, mapa, bússola, links e caminhos absolutos, e verificar por busca e navegação de wikilinks que as notas vigentes apontam para os novos artefatos.
- [x] 4.4 Atualizar ou adicionar os testes/documentação da capability `product-identity` e da preferência de aparência, e validar a change com `openspec validate rename-orderhub-to-zfome --strict`.

## 5. Validação integrada e localização do repositório

- [ ] 5.1 Executar restore, build e toda a suíte de testes de `ZFome.sln`, incluindo testes de integração, e confirmar ausência de erros e warnings relevantes.
- [ ] 5.2 Executar `dotnet format ZFome.sln --verify-no-changes`, repetir testes/typecheck/build do frontend e revisar `git diff --check` e todos os arquivos alterados.
- [x] 5.3 Executar busca residual case-insensitive em conteúdo e nomes de arquivo por `OrderHub`, eliminar dependências ativas e verificar individualmente que as únicas ocorrências restantes pertencem à allowlist histórica.
- [ ] 5.4 Após a validação do conteúdo, renomear o repositório GitHub para `zFome`, atualizar o remote `origin` e verificar fetch/push e a URL remota sem depender do redirecionamento antigo.
- [x] 5.5 Renomear o checkout local de `OrderHub` para `zFome`, reabrir as ferramentas no novo caminho e verificar novamente Git, OpenSpec, caminhos do Cérebro e comandos essenciais de build.

## Workflow follow-up

- Sincronizar a delta spec com as specs principais após a implementação e revisão.
- Arquivar a change somente depois que todos os itens rastreados e validações estiverem concluídos.
