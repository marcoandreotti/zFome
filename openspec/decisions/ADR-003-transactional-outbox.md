# ADR-003 — Outbox transacional no monólito

## Contexto

O zFome precisa publicar integrações derivadas de alterações persistidas, como notificações, sem perder mensagens quando o processo termina entre o commit do negócio e o envio externo. O ADR-001 estabeleceu que a solução permanece um monólito modular e que mensageria exige spec e ADR próprios. A change `add-outbox-pattern` especifica persistência atômica, processamento recuperável e operação sem broker externo.

## Problema

Gravar o estado de negócio e depois enviar uma mensagem em operações separadas cria uma janela de inconsistência: o banco pode confirmar a alteração e o processo falhar antes do envio, ou o envio pode ocorrer e a transação local falhar. Um broker externo também acrescentaria infraestrutura operacional sem necessidade comprovada nesta etapa.

## Opções

### Opção A — Envio direto após salvar

É simples, mas não recupera de forma confiável falhas entre a gravação local e o envio e pode bloquear o fluxo de negócio por indisponibilidade do fornecedor.

### Opção B — Publicação em broker externo

Desacopla produtores e consumidores, mas exige infraestrutura, credenciais, operação e tratamento de consistência distribuída antes de haver necessidade comprovada.

### Opção C — Outbox persistida no PostgreSQL e processada no monólito

Grava mensagens versionadas na mesma transação PostgreSQL da alteração local. Um worker no processo reivindica lotes com lease, tenta entrega com retry limitado e registra sucesso ou falha terminal. Consumidores são idempotentes porque uma entrega pode se repetir.

## Decisão

Adotar a opção C. A outbox pertence à Infrastructure, usa o PostgreSQL existente e é consumida por worker hospedado no monólito. Contratos de mensagem são versionados e independentes de entidades de domínio. Nenhum broker ou mensageria externa será introduzido por esta decisão.

## Consequências

### Positivas

- Uma mensagem só fica disponível quando a transação de negócio confirma.
- Mensagens pendentes podem ser recuperadas após reinício do processo.
- A primeira versão não exige infraestrutura distribuída adicional.
- Falhas e tentativas ficam disponíveis para diagnóstico e retenção controlada.

### Negativas

- O PostgreSQL armazena temporariamente mensagens ainda não entregues e exige política de retenção.
- A entrega é pelo menos uma vez; consumidores precisam de idempotência.
- O worker compartilha ciclo de vida e recursos com a API enquanto ambos estiverem no monólito.
- Concorrência de múltiplas instâncias depende de leasing atômico no banco.
