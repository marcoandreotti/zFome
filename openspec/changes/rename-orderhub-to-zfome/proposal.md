# Proposal

## Why

O nome OrderHub conflita com domínios e empresas já existentes, enquanto `zFome` foi confirmado pelo responsável pelo produto como disponível e comunica a ideia de “zero fome”. A renomeação deve acontecer antes da produção para consolidar uma identidade única sem carregar compatibilidade desnecessária com ambientes ou dados locais atuais.

## What Changes

- **BREAKING** Renomear a identidade oficial do produto de `OrderHub` para `zFome` em interfaces, metadados, documentação vigente e configurações operacionais.
- **BREAKING** Renomear solução, projetos, assemblies, namespaces, diretórios e testes .NET para o prefixo técnico `ZFome`, preservando `zFome` como grafia pública da marca.
- **BREAKING** Renomear o projeto web, o pacote frontend e identificadores técnicos próprios da aplicação para a nova identidade.
- **BREAKING** Substituir nomes locais de banco, usuário, rede, volume e projeto Docker; o ambiente atual poderá ser descartado e recriado, sem migração de dados ou compatibilidade com volumes anteriores.
- Atualizar identificadores internos de autenticação, observabilidade e armazenamento no navegador quando estiverem vinculados à marca antiga.
- Atualizar as fontes vigentes do repositório e o Cérebro do projeto no Obsidian, preservando referências históricas quando elas forem necessárias para descrever o passado.
- Renomear o repositório remoto e o diretório local somente após o conteúdo versionado estar consistente e validado.
- Manter arquitetura, bounded contexts, contratos HTTP e regras de negócio inalterados.

## Capabilities

### New Capabilities

- `product-identity`: Define `zFome` como a identidade oficial e exige consistência do nome público e dos identificadores técnicos mantidos pelo produto.

### Modified Capabilities

- `web/appearance-preferences`: Atualiza o cenário de retorno à aplicação para a identidade `zFome`, sem migrar a chave de preferência associada ao nome anterior.

## Impact

- Todos os projetos de produção e testes em `src/` e `tests/`, a solution, project references, Dockerfiles e testes arquiteturais.
- Aplicação Vue/Quasar em `web/OrderHub.Web`, incluindo título, pacote, persistência local e caminhos de build.
- Docker Compose, PostgreSQL local, configurações, nomes de serviços auxiliares e exemplos de ambiente.
- OpenSpec, ADRs e documentação vigente; notas e caminhos do Cérebro do projeto no repositório Obsidian.
- Repositório GitHub `marcoandreotti/OrderHub`, URL do remote `origin` e caminho do checkout local.
- Migrations EF Core e snapshot deverão continuar compilando e aplicando um banco novo; não haverá migração nem preservação do banco local existente.
- Nenhuma nova dependência, alteração arquitetural ou mudança de API de negócio é introduzida.
