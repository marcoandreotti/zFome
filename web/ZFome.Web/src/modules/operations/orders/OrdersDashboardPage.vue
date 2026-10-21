<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useSessionStore } from '../../session/store'
import { availableActions, orderStatusTone, serviceLabels, statusLabels, type OrderAction } from './actions'
import OperationsOrderCard from './OperationsOrderCard.vue'
import OperationsSyncStatus from './OperationsSyncStatus.vue'
import { isProductionDue, sortImmediateOrders, sortScheduledOrders } from './board'
import { PollingCoordinator } from './polling'
import {
  createOrderRealtimeConnection,
  OrderRealtimeCoordinator,
  type OrderRealtimeState
} from './realtime'
import { useOrderOperationsStore } from './store'
import type { OrderFilters, OrderServiceType, OrderStatus, OrderSummary } from './types'

const session = useSessionStore()
const store = useOrderOperationsStore()
const route = useRoute()
const router = useRouter()
const statusOptions = Object.entries(statusLabels).map(([value, label]) => ({ value, label }))
const serviceOptions = Object.entries(serviceLabels).map(([value, label]) => ({ value, label }))
const stateOrder: OrderStatus[] = ['Confirmed', 'Preparing', 'Ready', 'OutForDelivery', 'Completed', 'Cancelled', 'Rejected']
const status = ref<OrderStatus | undefined>(parseStatus(route.query.status))
const serviceType = ref<OrderServiceType | undefined>(parseService(route.query.serviceType) ?? (route.path.endsWith('/delivery') ? 'Delivery' : undefined))
const search = ref<string | null>(typeof route.query.search === 'string' ? route.query.search : '')
const defaultPeriod = lastTwoDays()
const fromDate = ref(readDate(route.query.from) ?? defaultPeriod.from)
const toDate = ref(readDate(route.query.to) ?? defaultPeriod.to)
const pendingAction = ref<OrderAction | null>(null)
const note = ref('')
const actionBusy = ref(false)
const detailPanel = ref<HTMLElement | null>(null)
const realtimeState = ref<OrderRealtimeState>('disconnected')

const periodValid = computed(() => !!readDate(fromDate.value) && !!readDate(toDate.value) && fromDate.value <= toDate.value)
const filters = computed<OrderFilters>(() => ({
  ...(periodValid.value ? { from: localDayStart(fromDate.value), to: localDayStart(addDays(toDate.value, 1)) } : {}),
  status: status.value,
  serviceType: serviceType.value,
  number: /^[1-9]\d*$/.test((search.value ?? '').trim()) ? Number(search.value) : undefined
}))
const grouped = computed(() =>
  stateOrder
    .filter((item) => !status.value || item === status.value)
    .map((item) => ({
      status: item,
      orders: store.orders.filter((order) => order.status === item),
      immediate: sortImmediateOrders(store.orders.filter((order) => order.status === item && !order.scheduledAtUtc)),
      scheduled: sortScheduledOrders(store.orders.filter((order) => order.status === item && !!order.scheduledAtUtc))
    }))
)
const selected = computed(() =>
  store.selectedId ? store.details[store.selectedId] : undefined
)
const selectedActions = computed(() =>
  selected.value
    ? availableActions(selected.value, session.context?.capabilities ?? [])
    : []
)
const cardAction = (order: OrderSummary) =>
  availableActions(order, session.context?.capabilities ?? [])[0]
const interval = Math.max(
  5_000,
  Number(import.meta.env.VITE_OPERATIONS_POLL_INTERVAL_MS) || 15_000
)
function promisedTime(order: Pick<OrderSummary, 'scheduledAtUtc' | 'scheduledTimeZoneId'>) {
  if (!order.scheduledAtUtc) return ''
  return new Intl.DateTimeFormat('pt-BR', {
    timeZone: order.scheduledTimeZoneId ?? undefined, dateStyle: 'short', timeStyle: 'short'
  }).format(new Date(order.scheduledAtUtc))
}
const poller = new PollingCoordinator(
  () => periodValid.value ? store.synchronize(session.unitId, filters.value) : Promise.resolve(),
  {
    intervalMs: interval,
    maxIntervalMs: Math.max(interval, 120_000),
    visibility: document
  }
)
const realtime = new OrderRealtimeCoordinator(
  createOrderRealtimeConnection(),
  poller,
  (state) => {
    realtimeState.value = state
  }
)

watch([status, serviceType, search, fromDate, toDate], () => {
  const query: Record<string, string> = {}
  if (status.value) query.status = status.value
  if (serviceType.value) query.serviceType = serviceType.value
  if (search.value?.trim()) query.search = search.value.trim()
  if (readDate(fromDate.value)) query.from = fromDate.value
  if (readDate(toDate.value)) query.to = toDate.value
  void router.replace({ query })
  if (periodValid.value) void poller.refresh(true)
})

watch(() => route.path, (path) => {
  if (path.endsWith('/delivery')) serviceType.value = 'Delivery'
  else if (serviceType.value === 'Delivery') serviceType.value = undefined
})

watch(
  () => session.unitId,
  (unit) => {
    store.reset(unit)
    void realtime.start(unit)
  }
)

onMounted(() => void realtime.start(session.unitId))
onBeforeUnmount(() => {
  void realtime.stop()
  store.reset()
})

async function openOrder(order: OrderSummary) {
  await store.select(session.unitId, order.id)
  await nextTick()
  detailPanel.value?.focus()
}

function requestAction(action: OrderAction) {
  if (action.destructive) {
    pendingAction.value = action
    note.value = ''
  } else void execute(action)
}

async function requestCardAction(order: OrderSummary, action: OrderAction) {
  await openOrder(order)
  requestAction(action)
}

function setConfirmation(open: boolean) {
  if (!open) pendingAction.value = null
}

async function execute(action = pendingAction.value) {
  if (!action || !selected.value) return
  actionBusy.value = true
  try {
    await store.transition(
      session.unitId,
      selected.value.id,
      action.transition,
      filters.value,
      note.value
    )
    pendingAction.value = null
  } catch {
    // O store preserva ProblemDetails e o estado autoritativo para a interface.
  } finally {
    actionBusy.value = false
    await nextTick()
    detailPanel.value?.focus()
  }
}

function parseStatus(value: unknown): OrderStatus | undefined {
  return typeof value === 'string' && value in statusLabels
    ? (value as OrderStatus)
    : undefined
}
function parseService(value: unknown): OrderServiceType | undefined {
  return typeof value === 'string' && value in serviceLabels
    ? (value as OrderServiceType)
    : undefined
}
function readDate(value: unknown): string | undefined {
  if (typeof value !== 'string' || !/^\d{4}-\d{2}-\d{2}$/.test(value)) return undefined
  const [year, month, day] = value.split('-').map(Number)
  const date = new Date(year, month - 1, day)
  return date.getFullYear() === year && date.getMonth() === month - 1 && date.getDate() === day ? value : undefined
}
function localDayStart(value: string) {
  const [year, month, day] = value.split('-').map(Number)
  return new Date(year, month - 1, day).toISOString()
}
function addDays(value: string, amount: number) {
  const [year, month, day] = value.split('-').map(Number)
  const date = new Date(year, month - 1, day + amount)
  return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`
}
function lastTwoDays() {
  const today = new Date()
  const to = `${today.getFullYear()}-${String(today.getMonth() + 1).padStart(2, '0')}-${String(today.getDate()).padStart(2, '0')}`
  return { from: addDays(to, -1), to }
}
function time(value: string | number) {
  return new Intl.DateTimeFormat('pt-BR', {
    hour: '2-digit',
    minute: '2-digit',
    day: typeof value === 'string' ? '2-digit' : undefined,
    month: typeof value === 'string' ? '2-digit' : undefined
  }).format(new Date(value))
}
function money(value: number) {
  return new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' }).format(value)
}
function elapsed(value: string) {
  const minutes = Math.max(0, Math.floor((Date.now() - Date.parse(value)) / 60_000))
  return minutes < 1 ? 'agora' : 'há ' + minutes + ' min'
}
function createdTimeLabel(value: string, orderStatus: OrderStatus) {
  const isHistorical = ['Completed', 'Cancelled', 'Rejected'].includes(orderStatus)
  return isHistorical
    ? new Intl.DateTimeFormat('pt-BR', { dateStyle: 'short', timeStyle: 'short' }).format(new Date(value))
    : elapsed(value)
}
</script>

<template>
  <q-page class="operations-page">
    <header class="operations-heading">
      <div>
        <h1 class="text-h4 q-my-none">{{ route.path.endsWith('/delivery') ? 'Entregas em andamento' : 'Pedidos em andamento' }}</h1>
      </div>
      <q-btn
        color="primary"
        unelevated
        no-caps
        label="Atualizar agora"
        :loading="store.loading"
        @click="poller.refresh(true)"
      />
    </header>

    <OperationsSyncStatus
      :has-unit="!!session.unitId"
      :realtime-state="realtimeState"
      :stale="store.stale"
      :error="store.error"
      :last-success-label="store.lastSuccessAt ? time(store.lastSuccessAt) : null"
      @retry="poller.refresh(true)"
    />

    <section class="operations-filters" aria-label="Filtros de pedidos">
      <q-input v-model="fromDate" outlined dense type="date" label="De" />
      <q-input v-model="toDate" outlined dense type="date" label="Até" />
      <q-select
        v-model="status"
        :options="statusOptions"
        emit-value
        map-options
        clearable
        outlined
        dense
        label="Estado"
      />
      <q-select
        v-model="serviceType"
        :options="serviceOptions"
        emit-value
        map-options
        clearable
        outlined
        dense
        label="Atendimento"
      />
      <q-input
        v-model="search"
        outlined
        dense
        clearable
        inputmode="numeric"
        label="Número do pedido"
      />
    </section>
    <q-banner v-if="!periodValid" class="bg-orange-1 text-brown-9 q-mb-md" role="status">
      Escolha um período válido: a data inicial deve ser igual ou anterior à data final.
    </q-banner>

    <q-banner v-if="!session.unitId" class="bg-blue-1 text-info">
      Selecione uma unidade autorizada para acompanhar os pedidos.
    </q-banner>

    <div v-else class="operations-workspace">
      <section class="status-board" aria-label="Pedidos por estado" :aria-busy="store.loading">
        <article
          v-for="group in grouped"
          :key="group.status"
          class="status-column"
          :class="`status-column--${orderStatusTone[group.status]}`"
        >
          <h2 :class="`status-heading--${orderStatusTone[group.status]}`">
            <span>{{ statusLabels[group.status] }}</span>
            <q-badge :label="group.orders.length" color="grey-8" />
          </h2>
          <h3 v-if="group.immediate.length && !['Completed', 'Cancelled', 'Rejected'].includes(group.status)" class="queue-subheading">Imediatos</h3>
          <OperationsOrderCard
            v-for="order in group.immediate"
            :key="order.id"
            :order="order"
            :service-label="serviceLabels[order.serviceType]"
            :time-label="createdTimeLabel(order.createdAt, order.status)"
            :total-label="money(order.total)"
            :selected="store.selectedId === order.id"
            :is-new="store.isNew(order.id)"
            :is-late="store.isLate(order)"
            :production-due="isProductionDue(order)"
            late-label="Atrasado"
            :next-action="cardAction(order)"
            @select="openOrder"
            @action="requestCardAction"
          />
          <h3 v-if="group.scheduled.length" class="queue-subheading">Agendados</h3>
          <OperationsOrderCard
            v-for="order in group.scheduled"
            :key="order.id"
            :order="order"
            :service-label="serviceLabels[order.serviceType]"
            :time-label="promisedTime(order)"
            :total-label="money(order.total)"
            :selected="store.selectedId === order.id"
            :is-new="store.isNew(order.id)"
            :is-late="store.isLate(order)"
            :production-due="isProductionDue(order)"
            late-label="Horário ultrapassado"
            :next-action="cardAction(order)"
            @select="openOrder"
            @action="requestCardAction"
          />
          <p v-if="!group.orders.length" class="empty-state">Nenhum pedido nesta etapa.</p>
        </article>
      </section>

      <aside
        v-if="selected"
        ref="detailPanel"
        tabindex="-1"
        class="order-detail"
        aria-label="Detalhes do pedido"
      >
        <div class="detail-heading">
          <div>
            <p class="text-overline q-mb-xs">{{ statusLabels[selected.status] }}</p>
            <h2 class="q-my-none">Pedido #{{ selected.number }}</h2>
          </div>
          <q-btn flat round aria-label="Fechar detalhes" @click="store.selectedId = null">×</q-btn>
        </div>

        <q-banner v-if="store.conflict" class="bg-orange-1 text-brown-9 q-my-md" role="alert">
          {{ store.conflict }}
        </q-banner>
        <q-banner v-if="store.actionError" class="bg-red-1 text-negative q-my-md" role="alert">
          {{ store.actionError }}
        </q-banner>

        <dl class="detail-facts">
          <div v-if="selected.scheduledAtUtc"><dt>Agendado para</dt><dd>{{ promisedTime(selected) }} · {{ selected.scheduledTimeZoneId }}</dd></div>
          <div><dt>Atendimento</dt><dd>{{ serviceLabels[selected.serviceType] }}</dd></div>
          <div v-if="selected.tableCode"><dt>Mesa</dt><dd>{{ selected.tableCode }}</dd></div>
          <div><dt>Pagamento</dt><dd>{{ selected.isFullyPaid ? 'Pago' : 'Pendente' }}</dd></div>
          <div><dt>Confirmado</dt><dd>{{ money(selected.confirmedAmount) }} de {{ money(selected.total) }}</dd></div>
        </dl>
        <section v-if="selected.serviceType === 'Delivery' && selected.deliveryAddress" class="q-mb-md" aria-label="Endereço de entrega">
          <h3 class="text-subtitle1">Destino da entrega</h3>
          <address>{{ selected.deliveryAddress.street }}, {{ selected.deliveryAddress.number }}<span v-if="selected.deliveryAddress.complement"> · {{ selected.deliveryAddress.complement }}</span><br>
            {{ selected.deliveryAddress.neighborhood }} · {{ selected.deliveryAddress.city }}/{{ selected.deliveryAddress.state }} · CEP {{ selected.deliveryAddress.postalCode }}</address>
          <small v-if="selected.deliveryRegionName">{{ selected.deliveryRegionName }} · {{ money(selected.deliveryFee) }} · estimativa {{ selected.deliveryEstimatedMinutes }} min</small>
        </section>

        <h3>Itens</h3>
        <ul class="detail-items">
          <li v-for="item in selected.items" :key="item.id">
            <strong>{{ item.quantity }}× {{ item.productName }}</strong>
            <span v-if="item.variationName"> · {{ item.variationName }}</span>
            <small v-if="item.additionals.length">
              + {{ item.additionals.map((value) => value.name).join(', ') }}
            </small>
            <p v-if="item.notes" class="item-note"><strong>Observação:</strong> {{ item.notes }}</p>
          </li>
        </ul>

        <h3>Histórico</h3>
        <ol class="order-history">
          <li v-for="entry in selected.history" :key="entry.occurredAt + entry.newStatus">
            <strong>{{ statusLabels[entry.newStatus] }}</strong>
            <time :datetime="entry.occurredAt">{{ time(entry.occurredAt) }}</time>
            <span v-if="entry.note">{{ entry.note }}</span>
          </li>
        </ol>

        <div v-if="selectedActions.length" class="detail-actions" aria-label="Ações do pedido">
          <q-btn
            v-for="action in selectedActions"
            :key="action.transition"
            square
            :color="action.destructive ? 'negative' : 'primary'"
            :outline="action.destructive"
            no-caps
            :icon="action.transition === 'prepare' ? 'play_arrow' : action.transition === 'reject' ? 'block' : action.transition === 'cancel' ? 'cancel' : action.transition === 'ready' ? 'check' : action.transition === 'dispatch' ? 'local_shipping' : 'task_alt'"
            :label="action.label"
            :loading="actionBusy"
            @click="requestAction(action)"
          />
        </div>
      </aside>
    </div>

    <q-dialog
      :model-value="pendingAction !== null"
      @update:model-value="setConfirmation"
    >
      <q-card class="confirmation-card">
        <q-card-section>
          <h2 class="text-h6 q-my-none">Confirmar {{ pendingAction?.label.toLowerCase() }}</h2>
          <p>O servidor verificará novamente seu papel e o estado atual do pedido.</p>
          <q-input v-model="note" autofocus outlined type="textarea" label="Motivo ou observação (opcional)" />
        </q-card-section>
        <q-card-actions align="right">
          <q-btn v-close-popup flat no-caps label="Voltar" />
          <q-btn :color="pendingAction?.destructive ? 'negative' : 'primary'" no-caps icon="check" :label="pendingAction?.label ? `Confirmar: ${pendingAction.label.toLowerCase()}` : 'Confirmar ação'" :loading="actionBusy" @click="execute()" />
        </q-card-actions>
      </q-card>
    </q-dialog>
  </q-page>
</template>

<style scoped>
.operations-page { min-height: calc(100vh - 72px); padding: 28px; background: var(--zf-surface-page); }
.operations-heading, .detail-heading { display: flex; justify-content: space-between; align-items: center; gap: 16px; }
.operations-heading { margin-bottom: 22px; }
.operations-heading h1 { color: var(--zf-text-primary); font-size: 1.85rem; font-weight: 750; letter-spacing: -.025em; }
.operations-filters {
  display: grid;
  grid-template-columns: repeat(5, minmax(145px, 1fr));
  gap: 12px;
  margin-bottom: 20px;
  padding: 14px;
  border: 1px solid var(--zf-border-subtle);
  border-radius: var(--zf-border-radius);
  background: var(--zf-surface-raised);
}
.operations-workspace { display: grid; grid-template-columns: minmax(0, 1fr) minmax(320px, 420px); gap: 20px; align-items: start; }
.status-board { display: grid; grid-template-columns: repeat(4, minmax(230px, 1fr)); gap: 16px; padding-bottom: 12px; }
.status-column {
  --status-accent: var(--zf-border-subtle);
  min-width: 0;
  min-height: 220px;
  padding: 14px;
  border: 1px solid var(--zf-border-subtle);
  border-radius: var(--zf-border-radius);
  background: var(--zf-surface-raised);
}
.status-column--confirmed { --status-accent: var(--zf-status-confirmed); }
.status-column--preparing { --status-accent: var(--zf-status-warning); }
.status-column--ready, .status-column--success { --status-accent: var(--zf-status-success); }
.status-column--info { --status-accent: var(--zf-status-info); }
.status-column--danger { --status-accent: var(--zf-status-danger); }
.status-column h2 { display: flex; align-items: center; gap: 8px; margin: 0 0 16px; font-size: 1rem; font-weight: 750; }
.status-column h2::before {
  content: '';
  flex: 0 0 8px;
  width: 8px;
  height: 8px;
  border-radius: 50%;
  background: var(--status-accent);
}
.status-column :deep(.q-badge) { min-width: 26px; justify-content: center; margin-left: auto; border-radius: 7px; }
.status-heading--confirmed { color: var(--zf-status-confirmed); }
.status-heading--preparing { color: var(--zf-status-warning-text); }
.status-heading--ready { color: var(--zf-status-success-text); }
.status-heading--success { color: var(--zf-status-success-text); }
.status-heading--info { color: var(--zf-status-info); }
.status-heading--danger { color: var(--zf-status-cancelled); }
.empty-state {
  margin: 0;
  padding: 14px 12px;
  border: 1px dashed var(--zf-border-subtle);
  border-radius: 8px;
  color: var(--zf-text-muted);
  font-size: .875rem;
}
.queue-subheading { margin: 12px 0 8px; font-size: .9rem; color: var(--zf-text-muted); }
.order-detail { position: sticky; top: 92px; max-height: calc(100vh - 116px); overflow: auto; padding: 20px; border-radius: var(--zf-border-radius); background: var(--zf-surface-raised); box-shadow: 0 8px 30px color-mix(in srgb, var(--zf-text-primary) 12%, transparent); }
.detail-facts { display: grid; grid-template-columns: 1fr 1fr; gap: 12px; }
.detail-facts div { padding: 10px; border-radius: 8px; background: var(--zf-surface-page); }
.detail-facts dt { color: var(--zf-text-muted); font-size: .75rem; }
.detail-facts dd { margin: 3px 0 0; font-weight: 700; }
.detail-items, .order-history { padding-left: 20px; }
.detail-items li, .order-history li { margin-bottom: 12px; }
.detail-items small, .order-history time, .order-history span { display: block; color: var(--zf-text-muted); }
.item-note { margin: 5px 0; padding: 7px; border-radius: 8px; background: color-mix(in srgb, var(--zf-status-warning) 12%, var(--zf-surface-raised)); }
.detail-actions { display: flex; flex-wrap: wrap; gap: 8px; position: sticky; bottom: -20px; margin: 20px -20px -20px; padding: 16px 20px; background: var(--zf-surface-raised); border-top: 1px solid var(--zf-border-subtle); }
.confirmation-card { width: min(92vw, 520px); }
@media (max-width: 1100px) {
  .operations-workspace { grid-template-columns: 1fr; }
  .status-board { grid-template-columns: repeat(2, minmax(230px, 1fr)); }
  .operations-filters { grid-template-columns: repeat(2, minmax(0, 1fr)); }
  .order-detail { position: static; max-height: none; }
}
@media (max-width: 700px) {
  .operations-page { padding: 20px 16px; }
  .operations-heading { align-items: flex-start; flex-direction: column; }
  .operations-heading h1 { font-size: 1.55rem; }
  .operations-filters { grid-template-columns: 1fr; }
  .status-board { grid-template-columns: 1fr; }
}
</style>
