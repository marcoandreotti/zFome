<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { useSessionStore } from '../../session/store'
import { createOrderRealtimeConnection, OrderRealtimeCoordinator } from '../orders/realtime'
import { PollingCoordinator } from '../orders/polling'
import { useKitchenDisplayStore } from './store'
import type { KitchenTicket } from './types'
import KitchenColumn from './KitchenColumn.vue'

const session = useSessionStore()
const store = useKitchenDisplayStore()
const now = ref(Date.now())
const realtimeState = ref('disconnected')
let clock: ReturnType<typeof setInterval> | undefined

const poller = new PollingCoordinator(
  () => store.synchronize(session.unitId),
  { intervalMs: 10_000, maxIntervalMs: 60_000, visibility: document }
)
const realtime = new OrderRealtimeCoordinator(
  createOrderRealtimeConnection(),
  poller,
  (state) => { realtimeState.value = state }
)

const columns = computed(() => [
  { key: 'waiting', title: 'Aguardando preparo', tickets: store.waiting },
  { key: 'preparing', title: 'Em preparo', tickets: store.preparing }
])
const syncSummary = computed(() => {
  const connection = realtimeState.value === 'connected'
    ? 'Tempo real conectado'
    : realtimeState.value === 'reconnecting'
      ? 'Reconectando ao tempo real · atualização periódica ativa'
      : realtimeState.value === 'connecting'
        ? 'Conectando ao tempo real · atualização periódica ativa'
        : 'Atualização periódica ativa'
  const lastSync = store.lastSuccessAt
    ? `Última sincronização: ${time(store.lastSuccessAt)}`
    : 'Sincronização ainda não concluída'
  return `Prioridade por etapa e ordem de confirmação · ${connection} · ${lastSync}`
})

watch(
  () => session.unitId,
  (unit) => {
    store.reset(unit)
    void realtime.start(unit)
  }
)

onMounted(() => {
  void realtime.start(session.unitId)
  clock = setInterval(() => { now.value = Date.now() }, 30_000)
})
onBeforeUnmount(() => {
  if (clock) clearInterval(clock)
  void realtime.stop()
  store.reset()
})

async function advance(ticket: KitchenTicket) {
  try {
    await store.execute(session.unitId, ticket)
  } catch {
    // O store mantém o ProblemDetails ou o snapshot reconciliado visível.
  }
}

function time(value: number) {
  return new Intl.DateTimeFormat('pt-BR', {
    hour: '2-digit',
    minute: '2-digit'
  }).format(new Date(value))
}
</script>

<template>
  <q-page class="kds-page">
    <header class="kds-heading">
      <div>
        <h1 class="text-h4 q-my-none"><span class="kds-title-brand">COZINHA</span> - Fila de produção</h1>
        <p class="sync-status q-mb-none" aria-live="polite">{{ syncSummary }}</p>
      </div>
      <q-btn
        outline
        color="primary"
        no-caps
        label="Atualizar agora"
        :loading="store.loading"
        @click="poller.refresh(true)"
      />
    </header>

    <q-banner v-if="store.stale" class="bg-orange-1 text-brown-9 q-mb-md" role="alert">
      <strong>Fila possivelmente desatualizada.</strong> {{ store.error }}
      <template #action>
        <q-btn flat no-caps label="Tentar novamente" @click="poller.refresh(true)" />
      </template>
    </q-banner>
    <q-banner v-if="store.conflict" class="bg-orange-1 text-brown-9 q-mb-md" role="alert">
      {{ store.conflict }}
    </q-banner>
    <q-banner v-if="store.actionError" class="bg-red-1 text-negative q-mb-md" role="alert">
      {{ store.actionError }}
    </q-banner>

    <q-banner v-if="!session.unitId" class="bg-blue-1 text-info">
      Selecione uma unidade autorizada para abrir a fila da cozinha.
    </q-banner>

    <section v-else class="kds-board" aria-label="Fila da cozinha" :aria-busy="store.loading">
      <KitchenColumn
        v-for="column in columns"
        :key="column.key"
        :column-key="column.key"
        :title="column.title"
        :tickets="column.tickets"
        :now="now"
        :busy-id="store.busyId"
        @advance="advance"
      />
    </section>
  </q-page>
</template>

<style scoped>
.kds-page { min-height: calc(100vh - 72px); padding: 20px; overflow-x: hidden; background: var(--zf-surface-page); }
.kds-heading { display: flex; justify-content: space-between; align-items: center; gap: 16px; margin-bottom: 12px; }
.kds-title-brand { color: var(--zf-brand-primary); }
.sync-status { margin-top: 4px; color: var(--zf-text-muted); }
.kds-board { display: grid; grid-template-columns: repeat(2, minmax(320px, 1fr)); gap: 20px; align-items: start; }
@media (max-width: 720px) {
  .kds-board { grid-template-columns: 1fr; }
}
@media (max-width: 600px) {
  .kds-page { padding: 14px; }
  .kds-heading { align-items: flex-start; flex-direction: column; }
}
</style>
