# Action Controls Specification

## Purpose

Define a consistent, recognizable and accessible presentation for contextual actions in repeated collections and for actions that confirm or submit changes across every zFome surface.

## Requirements

### Requirement: Ações de itens repetidos usam controles quadrados com ícone

Botões que executam ações sobre linhas, cards ou itens de uma lista ou grid SHALL usar formato quadrado e um ícone que represente a ação. Controles sem rótulo visível MUST expor nome acessível e dica de ação; ações operacionais cujo rótulo visível seja necessário à produção ou transição de estado MAY manter o texto junto ao ícone. A cor SHALL refletir a semântica da operação e MUST NOT ser o único sinal de seu efeito.

#### Scenario: Ação contextual em uma lista administrativa
- **WHEN** uma pessoa encontra ações de editar, abrir, ativar ou remover um item repetido
- **THEN** cada ação aparece como botão quadrado com ícone identificável, nome acessível e cor coerente com o efeito

#### Scenario: Operador aciona uma ação de card por touch ou teclado
- **WHEN** a ação de um pedido, ticket ou item é alcançada sem hover
- **THEN** ela continua visível, recebe foco perceptível e oferece uma área acionável adequada ao dispositivo

### Requirement: Ações de confirmação combinam ícone e rótulo

Botões primários que salvam, criam, enviam, confirmam ou avançam uma alteração SHALL apresentar um ícone à esquerda de um rótulo visível que nomeia a ação. O estado de carregamento e a condição desabilitada MUST permanecer perceptíveis, e a cor SHALL preservar a semântica existente da operação.

#### Scenario: Pessoa confirma uma alteração válida
- **WHEN** uma tela apresenta uma ação principal de confirmação ou envio
- **THEN** o botão apresenta ícone à esquerda, rótulo explícito e o estado de execução vigente

#### Scenario: Confirmação destrutiva
- **WHEN** a ação confirmada pode remover, cancelar ou desativar um recurso
- **THEN** o botão mantém o rótulo que explicita esse efeito e usa a semântica de perigo

#### Scenario: A pessoa cancela ou volta sem confirmar
- **WHEN** uma ação secundária apenas fecha, retorna ou cancela a confirmação
- **THEN** ela permanece visualmente secundária e não se confunde com o botão principal
