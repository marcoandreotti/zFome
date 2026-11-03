# Design

## Context

O nome atual aparece de forma transversal em assemblies, namespaces, caminhos, migrations EF Core, testes arquiteturais, frontend, Docker, configuração, documentação e no Cérebro do projeto. Consulte `proposal.md` para a motivação e `specs/product-identity/spec.md` para os comportamentos exigidos.

O produto ainda não está em produção e o ambiente PostgreSQL/Docker pode ser descartado. Portanto, o desenho não precisa manter dados, volumes, nomes de banco, preferências locais ou compatibilidade operacional com a identidade anterior. A arquitetura e os contratos de negócio permanecem inalterados.

## Goals / Non-Goals

**Goals:**

- aplicar uma convenção única de grafia por contexto;
- manter a mudança rastreável como renomes, reduzindo exclusões e recriações desnecessárias;
- preservar IDs de migrations e a evolução do schema para que um banco vazio receba toda a cadeia existente;
- garantir que testes arquiteturais continuem validando as mesmas fronteiras após o novo prefixo;
- tornar verificável a ausência da identidade antiga em artefatos ativos;
- coordenar repositório principal, Cérebro do projeto e renomeação remota sem deixar caminhos vigentes inválidos.

**Non-Goals:**

- preservar ou migrar dados, volumes e redes do ambiente local atual;
- migrar preferências armazenadas por versões anteriores do frontend;
- reescrever commits ou tags do histórico Git;
- alterar APIs de negócio, schemas PostgreSQL funcionais, bounded contexts ou decisões arquiteturais;
- redesenhar logotipo, paleta, tipografia ou experiência visual além da troca do nome textual;
- garantir retrocompatibilidade para consumidores externos ainda inexistentes.

## Decisions

### 1. Usar grafia específica por ecossistema

A apresentação pública usará `zFome`; artefatos .NET usarão `ZFome`; identificadores normalizados, como pacote npm, banco, usuário, rede, volume, chaves locais e nomes Docker, usarão `zfome` ou `zfome-*`.

Essa matriz preserva a grafia da marca sem contrariar PascalCase em .NET ou convenções lowercase de infraestrutura. A alternativa de usar literalmente `zFome` em todos os contextos foi rejeitada por produzir namespaces e nomes operacionais inconsistentes com seus ecossistemas.

### 2. Fazer o rename técnico como uma unidade coerente

Solution, projetos, diretórios versionados, assemblies, namespaces, project references, Dockerfiles e testes serão renomeados no mesmo incremento. Renomes de arquivos e pastas usarão operações reconhecíveis pelo Git, seguidos da atualização de conteúdo.

Separar assemblies e namespaces em entregas diferentes foi rejeitado porque deixaria estados intermediários que não compilam e aumentaria o risco de referências cruzadas à identidade anterior.

### 3. Preservar a cadeia de migrations, não o banco atual

As classes, namespaces, projeto e snapshot de migrations acompanharão `ZFome`, mas os IDs cronológicos existentes e as operações de schema serão preservados. O resultado será validado aplicando todas as migrations em um PostgreSQL vazio configurado como `zfome`.

Gerar uma única migration inicial substituta foi rejeitado porque apagaria a evolução verificável do schema sem benefício necessário. Migrar ou reaproveitar o volume atual também foi rejeitado porque o responsável pelo produto autorizou sua recriação.

### 4. Tratar identificadores internos como parte da nova identidade

Esquema de autenticação, nomes de métricas, título OpenAPI, pacote frontend, chaves de armazenamento local, remetentes de exemplo e outros identificadores próprios serão atualizados. Não haverá alias nem leitura de fallback para os valores antigos.

Aliases foram rejeitados porque ainda não há produção ou integrações externas que justifiquem uma camada de compatibilidade.

### 5. Atualizar fontes vigentes e preservar história sem reescrevê-la

AGENTS, projeto, arquitetura, convenções, specs principais, documentação operacional e Cérebro deverão usar a nova identidade e os caminhos atuais. ADRs e changes arquivadas poderão manter o nome anterior apenas quando descreverem explicitamente o estado histórico; referências operacionais dentro deles deverão ser corrigidas ou contextualizadas.

O histórico Git não será reescrito. Uma verificação final por texto usará uma allowlist explícita para qualquer ocorrência histórica remanescente, evitando tanto apagar contexto quanto deixar referências ativas por acidente.

### 6. Renomear checkout e remoto após validar o conteúdo

Primeiro será validado o conteúdo ainda no checkout atual. Em seguida, o repositório GitHub será renomeado para `zFome`, o `origin` será atualizado e o diretório local passará de `OrderHub` para `zFome`. Links e caminhos absolutos do Cérebro serão conferidos após a mudança física.

Renomear o remoto no início foi rejeitado porque dificulta distinguir problemas de conteúdo de problemas de localização durante a implementação.

### 7. Não criar novo ADR

A mudança altera identidade, não a decisão de arquitetura. O ADR-001 continua válido quanto ao monólito modular, camadas e projetos; apenas os nomes técnicos passam ao prefixo `ZFome`. A proposal, o design e a nova capability registram a decisão de nomenclatura sem introduzir uma segunda arquitetura.

## Risks / Trade-offs

- [Renomes em massa podem deixar referências quebradas ou duplicadas] → usar inventário antes/depois, busca case-insensitive e build completo.
- [Arquivos Designer e snapshot do EF contêm nomes qualificados antigos] → atualizar os nomes de tipo sem alterar IDs ou operações e validar migrations em banco vazio.
- [Mudança do nome Compose cria recursos novos] → aceitar a recriação, remover recursos antigos deliberadamente e documentar que não há preservação de dados.
- [Teste arquitetural pode passar sem analisar os assemblies renomeados] → atualizar nomes esperados e adicionar uma verificação explícita dos assemblies carregados.
- [Documentação histórica pode gerar falsos positivos] → exigir allowlist pequena e justificada em vez de ignorar diretórios inteiros.
- [Renomear o diretório local pode invalidar sessões e caminhos absolutos] → executar essa etapa por último e reabrir ferramentas no novo caminho.
- [GitHub mantém redirecionamento, mas integrações podem guardar a URL antiga] → atualizar `origin` e revisar configurações do repositório e integrações após o rename remoto.

## Migration Plan

1. Inventariar ocorrências ativas e definir a allowlist histórica inicial.
2. Renomear os artefatos .NET e atualizar namespaces, referências, migrations e testes arquiteturais.
3. Renomear frontend, identificadores internos, Docker, PostgreSQL e exemplos de configuração.
4. Atualizar documentação vigente, OpenSpec principal e Cérebro do projeto.
5. Remover o ambiente local antigo e validar a criação de um banco vazio pela cadeia completa de migrations.
6. Executar build e testes .NET, testes arquiteturais, lint/typecheck/test/build do frontend e smoke test do Compose.
7. Executar a busca residual case-insensitive por `OrderHub` e revisar cada ocorrência permitida.
8. Renomear o repositório GitHub, atualizar `origin`, renomear o checkout local e reconferir caminhos do Cérebro.

Rollback antes do rename remoto consiste em reverter o commit da mudança e recriar o ambiente local anterior, sem recuperação de dados. Após o rename remoto, o repositório pode voltar ao nome anterior, mas o rollback continuará sendo de código completo, não uma compatibilidade parcial entre identidades.
