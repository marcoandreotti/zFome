# Appearance Preferences Specification

## Purpose

Lets people choose a comfortable color appearance once and use it consistently across zFome's public, administrative, operational, kitchen, and platform views.

## Requirements

### Requirement: A pessoa escolhe a aparência da aplicação

A aplicação SHALL oferecer as opções Sistema, Claro e Escuro por meio de um controle acessível nas superfícies Public, Administration, Operations, KDS e Platform.

#### Scenario: Abrir o seletor de aparência
- **WHEN** a pessoa aciona o controle de aparência por teclado ou ponteiro
- **THEN** a aplicação apresenta as opções Sistema, Claro e Escuro, identifica a seleção atual por texto e mantém foco visível

### Requirement: A aparência escolhida vale em toda a aplicação

A aplicação SHALL aplicar a preferência selecionada a todas as superfícies e componentes compartilhados sem exigir recarregamento da página. Sistema SHALL acompanhar a preferência de aparência do dispositivo enquanto estiver selecionado.

#### Scenario: Primeira visita sem preferência salva
- **WHEN** a aplicação inicia em um navegador sem preferência explícita salva
- **THEN** a aparência segue a configuração atual do dispositivo

#### Scenario: A pessoa escolhe uma aparência fixa
- **WHEN** a pessoa seleciona Claro ou Escuro
- **THEN** todas as superfícies abertas e navegadas passam a usar a escolha imediatamente

#### Scenario: O dispositivo muda de aparência
- **WHEN** Sistema está selecionado e a preferência do dispositivo muda
- **THEN** a aplicação acompanha a nova aparência sem substituir a opção Sistema

### Requirement: A preferência permanece no navegador

A aplicação SHALL persistir a opção selecionada no navegador atual e restaurá-la em visitas e superfícies futuras. Uma escolha feita em um navegador MUST NOT alterar preferências de conta ou de outros dispositivos.

#### Scenario: Retornar à aplicação
- **WHEN** a pessoa abre novamente o zFome no mesmo navegador
- **THEN** a última opção selecionada é restaurada antes da interação com a interface

### Requirement: A aparência preserva identidade e contraste

A aplicação MUST adaptar superfícies neutras, texto, bordas, campos e sobreposições ao modo claro ou escuro, preservando o contraste e os estados semânticos. A superfície pública MUST preservar a identidade do estabelecimento, incluindo marca e cor primária, sem permitir que cores personalizadas comprometam a leitura no modo selecionado.

#### Scenario: Tema público em modo escuro
- **WHEN** uma pessoa escolhe Escuro em um cardápio tematizado por um estabelecimento
- **THEN** os neutros públicos usam a aparência escura, a marca do estabelecimento permanece reconhecível e texto, controles e ações continuam legíveis

#### Scenario: Estados operacionais em modo escuro
- **WHEN** pedidos, alertas ou ações são exibidos no modo escuro
- **THEN** estados continuam identificáveis por texto ou sinal visual além da cor e mantêm contraste adequado
