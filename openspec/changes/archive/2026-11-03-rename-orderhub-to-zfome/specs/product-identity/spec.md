# Spec Delta

## Purpose

Define a identidade oficial do produto e garante que superfícies públicas, artefatos mantidos e ambientes reproduzíveis apresentem a marca zFome de forma consistente.

## ADDED Requirements

### Requirement: Identidade pública oficial

As superfícies e os metadados mantidos pelo produto SHALL apresentar `zFome` como nome oficial da plataforma e MUST NOT apresentar `OrderHub` como identidade atual.

#### Scenario: Pessoa acessa uma superfície da plataforma
- **WHEN** uma pessoa abre uma página, documento de API ou outro ponto mantido que identifique a plataforma
- **THEN** o nome apresentado da plataforma é `zFome`
- **AND** `OrderHub` não é apresentado como a marca atual

#### Scenario: Identidade do estabelecimento na superfície pública
- **WHEN** uma pessoa acessa uma experiência pública tematizada por um estabelecimento
- **THEN** a identidade própria do estabelecimento continua sendo preservada
- **AND** qualquer identificação da plataforma usa `zFome`

### Requirement: Identidade técnica coerente

Os artefatos técnicos mantidos pelo produto SHALL usar `ZFome` em identificadores .NET, `zFome` em texto de marca e `zfome` em identificadores normalizados que não diferenciam apresentação tipográfica.

#### Scenario: Compilação e testes da solução
- **WHEN** a solução e os testes forem restaurados, compilados e executados após a renomeação
- **THEN** projetos, assemblies, namespaces e referências mantidos usam o prefixo `ZFome`
- **AND** as verificações arquiteturais continuam protegendo as mesmas fronteiras

#### Scenario: Verificação de identificadores ativos
- **WHEN** o conteúdo vigente do produto for verificado por referências à identidade anterior
- **THEN** nenhum identificador operacional ou artefato mantido depende de `OrderHub`
- **AND** ocorrências preservadas existem somente em registros explicitamente históricos

### Requirement: Ambiente local recriável com a nova identidade

O ambiente local SHALL poder ser recriado do zero usando identificadores `zfome`, sem exigir banco, volume, rede ou configuração legada da identidade anterior.

#### Scenario: Inicialização limpa do ambiente
- **WHEN** um desenvolvedor remove o ambiente local anterior e inicia a composição atualizada
- **THEN** banco, migrations, API e aplicação web iniciam usando a nova configuração
- **AND** todas as migrations existentes são aplicadas com sucesso em um banco vazio

#### Scenario: Preferência antiga no navegador
- **WHEN** um navegador possui apenas uma chave local associada à identidade anterior
- **THEN** a aplicação inicia com a preferência padrão sob a nova identidade
- **AND** nenhuma migração da chave anterior é exigida

### Requirement: Documentação vigente acompanha a nova identidade

A documentação normativa e operacional vigente SHALL identificar o produto como `zFome`, enquanto registros históricos MAY conservar o nome anterior quando necessário para manter seu significado original.

#### Scenario: Desenvolvimento orientado pela documentação
- **WHEN** uma pessoa segue as instruções vigentes para localizar, compilar, testar ou executar o produto
- **THEN** os nomes e caminhos documentados correspondem aos artefatos renomeados

#### Scenario: Consulta de registro histórico
- **WHEN** uma pessoa consulta um commit, change arquivada ou decisão histórica preservada
- **THEN** referências à identidade anterior podem permanecer quando estiver claro que descrevem o estado anterior à renomeação
