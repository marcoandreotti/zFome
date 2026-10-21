# Spec Delta

## ADDED Requirements

### Requirement: Pedido elegível oferece convite de avaliação
A tela de acompanhamento SHALL oferecer avaliação somente quando o pedido estiver concluído e dentro da janela de 30 dias, sem bloquear o acompanhamento.

#### Scenario: Pedido concluído no prazo
- **WHEN** o servidor informa pedido concluído ainda elegível
- **THEN** o visitante pode abrir a avaliação a partir do acompanhamento

#### Scenario: Pedido ainda não elegível ou prazo expirado
- **WHEN** o pedido não está concluído ou passou da janela de avaliação
- **THEN** a tela mantém o acompanhamento e não oferece envio de avaliação

### Requirement: Nota usa controle acessível de estrelas
A entrada e exibição de nota SHALL usar o componente de avaliação Quasar com nota textual equivalente, rótulo acessível e suporte a teclado.

#### Scenario: Cliente seleciona nota baixa
- **WHEN** o cliente seleciona nota 1 ou 2
- **THEN** o formulário solicita comentário de 10 a 1000 caracteres e explica o requisito

### Requirement: Classificação pública comunica média e volume
O cardápio SHALL exibir média e contagem quando existirem avaliações publicadas para a unidade ou produto.

#### Scenario: Unidade e produto sem avaliações
- **WHEN** não há avaliações publicadas
- **THEN** a interface omite a nota em vez de exibir zero
