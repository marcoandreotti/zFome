# Customer Records Specification

## Purpose

Define clientes e endereços por unidade com baixa fricção de compra e preservação do isolamento de dados.

## Requirements

### Requirement: Cliente pode comprar sem conta autenticada
O sistema SHALL permitir registrar cliente da unidade com nome e telefone, sem exigir senha ou confirmação de e-mail para iniciar um pedido.

#### Scenario: Cliente sem e-mail
- **WHEN** um visitante fornecer nome e telefone válidos sem e-mail
- **THEN** o sistema SHALL permitir seu uso no fluxo de pedido

### Requirement: Dados do cliente são isolados por unidade
Clientes MUST pertencer a uma unidade e MUST NOT ser pesquisados, vinculados ou alterados por outra unidade apenas por coincidência de telefone, e-mail ou documento.

#### Scenario: Telefones iguais em unidades distintas
- **WHEN** unidades diferentes registrarem o mesmo telefone
- **THEN** o sistema SHALL manter registros independentes e isolados

### Requirement: Cliente mantém endereços rotulados
Um cliente MAY possuir múltiplos endereços completos e SHALL possuir no máximo um endereço principal por vez.

#### Scenario: Novo endereço principal
- **WHEN** outro endereço do cliente for definido como principal
- **THEN** o sistema SHALL remover a marca principal do endereço anterior de forma consistente

### Requirement: Contatos de cliente são normalizados por estabelecimento
O sistema SHALL normalizar nome, telefone e e-mail opcional antes da persistência e MUST aplicar qualquer critério de unicidade somente no escopo do mesmo estabelecimento.

Telefones brasileiros com 10 ou 11 dígitos nacionais SHALL usar a forma nacional sem código do país `55` e sem prefixo de tronco `0`. Formas legadas equivalentes SHALL ser localizadas no mesmo estabelecimento sem mesclar ou remover registros automaticamente; se mais de um registro corresponder, a operação SHALL retornar conflito.

#### Scenario: Mesmo telefone no mesmo estabelecimento
- **WHEN** um cliente já registrado for identificado pelo telefone normalizado dentro do mesmo estabelecimento
- **THEN** o sistema SHALL atualizar ou reutilizar esse registro conforme a operação solicitada sem criar duplicação acidental

#### Scenario: Telefone brasileiro com código do país e prefixo de tronco
- **WHEN** `+55 (0)19 99999-9999` e `19999999999` forem usados no mesmo estabelecimento
- **THEN** o sistema SHALL normalizar ambos para `19999999999` e reutilizar o registro correspondente

#### Scenario: Registros legados equivalentes ambíguos
- **WHEN** mais de um registro legado do estabelecimento corresponder ao mesmo telefone brasileiro normalizado
- **THEN** o sistema SHALL retornar conflito sem mesclar nem remover registros

### Requirement: Endereços pertencem ao cliente e ao mesmo estabelecimento
Cada endereço MUST pertencer ao mesmo Tenant e estabelecimento do cliente, SHALL conter os dados necessários para entrega e MUST impedir associação cruzada.

#### Scenario: Endereço de cliente de outra unidade
- **WHEN** uma operação tentar associar ao cliente um endereço pertencente a outra unidade
- **THEN** o sistema MUST rejeitar a operação sem alterar os registros existentes

### Requirement: Alterações de cliente e endereço são consistentes
Uma operação que alterar cliente, endereços e endereço principal como um único efeito SHALL ser atômica.

#### Scenario: Falha ao definir endereço principal
- **WHEN** a persistência falhar durante a troca do endereço principal
- **THEN** o sistema SHALL preservar o cliente e a configuração de endereços anteriores integralmente

### Requirement: Fluxo público mantém cliente no estabelecimento resolvido
A API pública SHALL localizar ou registrar cliente e endereço somente dentro do estabelecimento resolvido e MUST NOT permitir consulta geral de clientes por contato.

#### Scenario: Telefone existe em outra unidade
- **WHEN** um visitante informar telefone registrado apenas em outro estabelecimento
- **THEN** a API MUST tratar os dados de forma independente sem revelar o registro externo

### Requirement: Administração pesquisa clientes da unidade autorizada
A API SHALL permitir pesquisa paginada por nome, telefone ou e-mail normalizados somente no estabelecimento autorizado e SHALL permitir manutenção de seus endereços.

#### Scenario: Pesquisa por telefone
- **WHEN** um ator autorizado pesquisar telefone na unidade selecionada
- **THEN** somente clientes daquela unidade SHALL compor o resultado
