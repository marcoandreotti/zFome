<script setup lang="ts">
import type { PublicContext } from './types'
import type { RatingSummary } from './client'

defineProps<{
  context: PublicContext
  summary?: RatingSummary | null
}>()
</script>

<template>
  <header class="ordering-hero">
    <img v-if="context.theme.logoUrl" :src="context.theme.logoUrl" alt="" class="ordering-logo">
    <span v-else class="ordering-unit-mark" aria-hidden="true">
      <q-icon name="storefront" />
    </span>
    <div class="ordering-context">
      <p class="ordering-unit-label">Cardápio da unidade</p>
      <h1>{{ context.establishmentName }}</h1>
      <p>Explore o cardápio e monte seu pedido.</p>
    </div>
    <span v-if="context.table" class="ordering-table-badge">
      <q-icon name="table_restaurant" aria-hidden="true" /> Mesa {{ context.table.code }}
    </span>
    <span v-if="summary" class="ordering-rating" :aria-label="`Nota média ${summary.average.toLocaleString('pt-BR', { maximumFractionDigits: 1 })} de 5, ${summary.count} avaliações`">
      <q-icon name="star" aria-hidden="true" /> {{ summary.average.toLocaleString('pt-BR', { maximumFractionDigits: 1 }) }} <small>{{ summary.count }} avaliações</small>
    </span>
  </header>
</template>

<style scoped>
.ordering-rating { display: flex; align-items: center; gap: 5px; color: var(--zf-text-primary); font-weight: 700; }
.ordering-rating :deep(.q-icon) { color: #b77900; }
.ordering-rating small { color: var(--zf-text-muted); font-weight: 400; }
</style>
