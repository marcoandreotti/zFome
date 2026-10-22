<script setup lang="ts">
import type { OrderRealtimeState } from './realtime'

defineProps<{
  hasUnit: boolean
  realtimeState: OrderRealtimeState
  stale: boolean
  error: string
  lastSuccessLabel: string | null
}>()

defineEmits<{ retry: [] }>()
</script>

<template>
  <section v-if="hasUnit" class="operations-sync" aria-label="Atualização da fila">
    <q-banner v-if="stale" class="bg-orange-1 text-brown-9" role="alert">
      <strong>Dados possivelmente desatualizados.</strong>
      {{ error }} A fila carregada foi preservada.
      <template #action>
        <q-btn flat no-caps label="Tentar novamente" @click="$emit('retry')" />
      </template>
    </q-banner>
    <q-banner v-else-if="realtimeState !== 'connected'" class="bg-blue-1 text-info" aria-live="polite">
      <strong>{{ realtimeState === 'reconnecting' ? 'Reconectando ao tempo real.' : 'Canal em tempo real indisponível.' }}</strong>
      A fila permanece visível e usa atualização periódica como fallback.
    </q-banner>
    <p class="sync-status" aria-live="polite">
      <span v-if="realtimeState === 'connected'">Tempo real conectado · reconciliação automática</span>
      <span v-else-if="realtimeState === 'connecting'">Conectando ao tempo real · atualização periódica ativa</span>
      <span v-else-if="realtimeState === 'reconnecting'">Reconectando ao tempo real · atualização periódica ativa</span>
      <span v-else>Tempo real indisponível · atualização periódica ativa</span>
      <span> · {{ lastSuccessLabel ? `Última sincronização: ${lastSuccessLabel}` : 'Sincronização ainda não concluída' }}</span>
    </p>
  </section>
</template>

<style scoped>
.operations-sync { display: grid; gap: 8px; margin-bottom: 16px; }
.sync-status { min-height: 24px; margin: 0; color: var(--zf-text-muted); }
</style>
