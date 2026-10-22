# Customer Reviews Specification

## Purpose

Lets a customer with a completed order submit verified ratings and lets visitors and authorized staff use published reviews and their derived ratings.

## Requirements

### Requirement: Nota de avaliação é inteira de 1 a 5
O sistema SHALL aceitar somente notas inteiras de 1 a 5 para estabelecimento e produto.

#### Scenario: Nota inválida
- **WHEN** uma nota ausente, fracionada ou fora de 1–5 é enviada
- **THEN** a requisição é rejeitada sem criar avaliação

### Requirement: Nota baixa exige comentário
Notas 1 e 2 SHALL exigir comentário não vazio após normalização, entre 10 e 1000 caracteres; notas 3–5 MAY omiti-lo.

#### Scenario: Nota baixa sem comentário
- **WHEN** nota 1 ou 2 é enviada sem comentário ou com apenas espaços
- **THEN** a avaliação é rejeitada sem persistência

#### Scenario: Nota alta sem comentário
- **WHEN** nota 3, 4 ou 5 é enviada sem comentário
- **THEN** a avaliação é aceita se os demais requisitos forem atendidos

### Requirement: Avaliação exige pedido concluído e dentro do prazo
Uma avaliação SHALL referenciar pedido concluído há no máximo 30 dias e SHALL usar exclusivamente a unidade e os produtos desse pedido.

#### Scenario: Pedido não concluído ou fora do prazo
- **WHEN** o pedido não está concluído ou sua conclusão ocorreu há mais de 30 dias
- **THEN** a publicação é rejeitada

#### Scenario: Produto não comprado
- **WHEN** o payload inclui produto que não pertence à unidade ou não consta no pedido
- **THEN** a publicação é rejeitada e nenhum alvo é registrado

### Requirement: Uma avaliação por pedido e alvo
O sistema SHALL aceitar no máximo uma avaliação por pedido para o estabelecimento e uma por produto avaliado; avaliação publicada não pode ser editada nem excluída.

#### Scenario: Alvo repetido
- **WHEN** o mesmo pedido tenta avaliar novamente um alvo já avaliado
- **THEN** o sistema responde conflito e preserva o registro existente

### Requirement: Publicação é idempotente
A publicação SHALL exigir chave de idempotência; repetição da mesma chave e payload SHALL devolver o resultado original sem duplicar avaliações.

#### Scenario: Repetição de envio
- **WHEN** a mesma chave e payload são reenviados após perda da resposta
- **THEN** a API devolve o resultado original

#### Scenario: Chave reutilizada com payload diferente
- **WHEN** uma chave já usada é reenviada com payload diferente
- **THEN** a API responde conflito e não altera avaliações

### Requirement: Avaliação aguarda moderação antes de ficar pública
Toda avaliação submetida SHALL iniciar pendente e MUST permanecer fora das consultas e classificações públicas até aprovação por equipe autorizada.

As transições administrativas SHALL distinguir `Rejected` (reprovação de uma avaliação pendente) de `Hidden` (ocultação de conteúdo publicado). Somente uma avaliação `Hidden` pode ser restaurada; avaliações `Rejected` não podem ser publicadas por restauração.

#### Scenario: Envio aguardando revisão
- **WHEN** o consumidor envia uma avaliação válida
- **THEN** o sistema registra o conteúdo como pendente e não o exibe publicamente nem o inclui nos resumos

#### Scenario: Equipe aprova
- **WHEN** gerente autorizado aprova uma avaliação pendente
- **THEN** o estado muda para publicada, a ação fica auditada e o resumo é atualizado na mesma transação

### Requirement: Autor é exibido anonimamente
A consulta pública SHALL omitir nome, telefone, e-mail, identificador interno e referência do pedido do autor.

#### Scenario: Leitura pública
- **WHEN** um visitante consulta comentários publicados
- **THEN** a resposta não identifica o consumidor nem expõe a referência que comprova a compra

### Requirement: Classificação deriva de avaliações publicadas
Média, contagem e distribuição SHALL incluir apenas registros publicados e refletir aprovação, ocultação ou restauração na mesma transação.

#### Scenario: Avaliação ocultada
- **WHEN** a equipe oculta uma avaliação
- **THEN** ela deixa imediatamente de compor os indicadores públicos e administrativos agregados

#### Scenario: Avaliação restaurada
- **WHEN** a equipe restaura uma avaliação
- **THEN** ela volta imediatamente a compor os indicadores

### Requirement: Equipe modera sem editar nota
A equipe com capacidade de gerenciamento SHALL aprovar e reprovar avaliações pendentes, e ocultar e restaurar avaliações publicadas com histórico; ocultar/reprovar exige motivo, e operações de edição de nota e exclusão não são permitidas. Uma avaliação reprovada MUST permanecer não pública e não restaurável.

#### Scenario: Ocultar com motivo
- **WHEN** um gerente autorizado oculta uma avaliação com motivo não vazio
- **THEN** o comentário deixa a exibição pública e a ação fica registrada com autor e instante

#### Scenario: Tentativa sem autorização ou motivo
- **WHEN** ator sem capacidade ou gerente sem motivo solicita ocultação
- **THEN** a API rejeita a operação e não muda estado

### Requirement: Acesso público permanece isolado por unidade
Rotas públicas SHALL resolver unidade pelo slug ou referência opaca do pedido e MUST filtrar toda leitura/escrita por tenant e unidade do servidor.

#### Scenario: Referência de outra unidade
- **WHEN** uma consulta usa referência pertencente a outra unidade
- **THEN** não retorna dados de avaliação fora do escopo resolvido
