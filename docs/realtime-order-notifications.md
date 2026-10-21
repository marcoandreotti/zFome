# Notificações de pedidos em tempo real

O painel operacional usa SignalR como mecanismo primário de invalidação e mantém
a consulta HTTP como fonte autoritativa. Eventos não substituem queries, podem ser
duplicados e não são usados para reconstruir estado.

## Canal e contrato

- Hub autenticado: `/hubs/order-updates`.
- Inscrição: `SubscribeAsync(establishmentId)`.
- Evento cliente: `OrderUpdated`.
- Contrato atual: `OrderUpdatedMessageV1`, com `version = 1`, `orderId`,
  `changeType`, `establishmentId` e `occurredAt`.
- O contrato não transporta `TenantId`, entidades de domínio ou dados pessoais.

O servidor deriva Tenant e usuário da sessão autenticada. A unidade solicitada é
validada pelo `EstablishmentScopeResolver`; o cliente não escolhe o Tenant. Antes
de publicar, associações administrativas são revalidadas. Conexões cuja associação
foi revogada são removidas do grupo antes da entrega.

## Persistência e recuperação

Os handlers publicam somente após `SaveChangesAsync` ou commit explícito:

- confirmação administrativa;
- confirmação pelo pedido público;
- transições operacionais;
- cancelamento público.

Falha do canal após o commit é registrada e não transforma uma operação já
persistida em erro HTTP. Ao conectar ou reconectar, o painel recarrega a query de
pedidos antes de indicar que está sincronizado. Se SignalR estiver indisponível,
o polling com backoff permanece ativo, pausa em aba oculta e nunca sobrepõe ciclos.

## Configuração

API:

- `RealtimeOrders__ClientTimeoutSeconds` (padrão: 30);
- `RealtimeOrders__KeepAliveSeconds` (padrão: 15);
- `RealtimeOrders__MaximumReceiveMessageBytes` (padrão: 32768).

Web:

- `VITE_REALTIME_BASE_URL`: origem opcional do hub;
- sem essa variável, usa `VITE_API_BASE_URL` ou a origem atual;
- `VITE_OPERATIONS_POLL_INTERVAL_MS`: intervalo inicial do fallback.

O Nginx encaminha `/hubs/` com upgrade WebSocket. O proxy de desenvolvimento do
Quasar também habilita WebSocket para esse caminho.

## Telemetria

O meter `ZFome.Api.RealtimeOrders` publica:

- `zfome.realtime.connections`;
- `zfome.realtime.subscriptions`;
- `zfome.realtime.events.published`;
- `zfome.realtime.deliveries.denied`;
- `zfome.realtime.failures`.

Logs estruturados registram conexão, inscrição, revogação e falha de publicação,
sem incluir conteúdo do pedido ou credenciais.

## Implantação e rollback

1. Implantar a API com o hub antes do frontend.
2. Confirmar suporte a WebSocket no proxy e CORS com credenciais para a origem web.
3. Implantar o frontend e acompanhar falhas/conexões do meter.
4. Para rollback do frontend, remover ou desabilitar o uso do cliente SignalR; o
   polling continua compatível.
5. Para rollback da API, primeiro restaurar o frontend que usa somente polling.

Não há alteração de banco nem mensageria externa nesta change.
