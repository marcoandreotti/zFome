## Purpose

Oferece atendimento conversacional sobre o zFome mantendo preços, disponibilidade, autorização e confirmação sob controle dos serviços determinísticos.

## ADDED Requirements

### Requirement: Agente usa somente ferramentas públicas autorizadas
O agente MUST acessar catálogo, disponibilidade e carrinho por contratos tenant-scoped e MUST NOT inventar preços, itens, descontos ou estados de pedido.

#### Scenario: Resposta do modelo diverge da ferramenta
- **WHEN** texto gerado contradiz o resultado autoritativo
- **THEN** nenhuma mutação usa o texto como fonte de decisão

### Requirement: Compromisso comercial exige confirmação explícita
O sistema MUST apresentar resumo autoritativo e obter confirmação do cliente antes de confirmar pedido ou executar ação irreversível.

#### Scenario: Conversa termina sem confirmação
- **WHEN** o cliente abandona o atendimento
- **THEN** nenhum pedido confirmado ou cobrança é criado
