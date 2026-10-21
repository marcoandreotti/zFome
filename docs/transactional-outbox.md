# Outbox transacional

O publisher (`IOutboxMessageStager`) deve receber mensagens depois de validar o caso de uso e antes do `SaveChangesAsync` da mesma unidade de trabalho. Ele apenas adiciona a mensagem ao `zFomeDbContext`; a transação de negócio grava aggregate e outbox juntos. Não chame o fornecedor externo dentro do command handler.

Consumidores implementam `IOutboxMessageHandler` para um `MessageType` e `SchemaVersion` específicos, e usam um `ConsumerName` estável. O worker grava o recibo do consumidor, os efeitos locais e o acknowledgement na mesma transação PostgreSQL. Um handler que chama um serviço externo também deve enviar `IdempotencyKey` ao fornecedor, pois uma falha de rede pode deixar incerto se o efeito externo ocorreu.

## Configuração

As opções ficam em `Outbox` e têm estes padrões:

| Opção | Padrão | Uso |
| --- | ---: | --- |
| `BatchSize` | 50 | Limite de mensagens reivindicadas por lote |
| `MaxAttempts` | 10 | Tentativas até mover a mensagem para falha terminal |
| `LeaseDurationSeconds` | 60 | Duração do lease de uma mensagem reivindicada |
| `PollingIntervalSeconds` | 2 | Pausa entre lotes quando a fila está vazia ou parcial |
| `InitialRetryDelaySeconds` | 5 | Atraso inicial do retry exponencial |
| `MaximumRetryDelaySeconds` | 3600 | Limite do atraso entre retries |
| `RetentionDays` | 30 | Retenção após sucesso ou falha terminal |
| `CleanupIntervalHours` | 12 | Frequência de limpeza de mensagens expiradas |

O atraso cresce exponencialmente até o máximo configurado. Valores fora dos limites validados impedem a inicialização da aplicação.

## Diagnóstico e recuperação

Consulte apenas metadados e filtre por Tenant e identificador da mensagem. Evite selecionar `payload_json`, que pode conter dados pessoais.

```sql
select id, tenant_id, message_type, schema_version, idempotency_key,
       attempt_count, next_attempt_at_utc, lease_until_utc,
       processed_at_utc, dead_lettered_at_utc, last_error
from integration.outbox_message
where tenant_id = '<tenant-id>'::uuid
order by created_at_utc desc
limit 100;
```

`last_error` guarda apenas o tipo da exceção; o worker registra a mesma classe de erro nos logs. Mensagens sem consumidor para seu tipo e versão também seguem retry e terminam em falha terminal. Registre o handler compatível antes de reprocessar.

Depois de corrigir a causa e confirmar que o consumidor/fornecedor preserva a idempotência, uma mensagem terminal pode ser reprogramada pelo operador:

```sql
update integration.outbox_message
set dead_lettered_at_utc = null,
    attempt_count = 0,
    next_attempt_at_utc = now(),
    lease_token = null,
    lease_until_utc = null,
    last_error = null
where tenant_id = '<tenant-id>'::uuid
  and id = '<message-id>'::uuid
  and dead_lettered_at_utc is not null
returning id, tenant_id, message_type, schema_version, idempotency_key;
```

Não gere outra chave de idempotência ao recuperar uma mensagem. Mensagens processadas e terminais são removidas após a retenção configurada; os recibos de consumidores são removidos em cascata.
