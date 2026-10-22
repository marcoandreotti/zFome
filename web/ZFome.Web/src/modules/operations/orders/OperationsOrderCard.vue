<script setup lang="ts">
import type { OrderAction } from './actions'
import type { OrderSummary } from './types'

import { computed } from 'vue'

const props = defineProps<{
  order: OrderSummary
  serviceLabel: string
  timeLabel: string
  totalLabel: string
  selected: boolean
  isNew: boolean
  isLate: boolean
  lateLabel: string
  productionDue?: boolean
  nextAction?: OrderAction
}>()

const actionIcons: Record<OrderAction['transition'], string> = {
  prepare: 'play_arrow',
  reject: 'block',
  cancel: 'cancel',
  ready: 'check',
  dispatch: 'local_shipping',
  complete: 'task_alt'
}
const nextActionIcon = computed(() => props.nextAction
  ? actionIcons[props.nextAction.transition]
  : undefined)

defineEmits<{
  select: [order: OrderSummary]
  action: [order: OrderSummary, action: OrderAction]
}>()
</script>

<template>
  <article class="order-card-shell" :class="{ selected, scheduled: !!order.scheduledAtUtc, due: productionDue, late: isLate }">
    <button class="order-card" type="button"
      :aria-label="`Pedido ${order.number}, ${serviceLabel}, ${timeLabel}`"
      @click="$emit('select', order)">
      <span class="order-card-title">
        <strong>#{{ order.number }}</strong>
        <span>{{ timeLabel }}</span>
      </span>
      <span>{{ serviceLabel }}<span v-if="order.scheduledAtUtc"> · Horário prometido</span></span>
      <span v-if="order.customerName">{{ order.customerName }}</span>
      <span class="order-card-items">
        <span v-for="(item, index) in order.items ?? []" :key="`${item.productName}:${index}`">
          <strong>{{ item.quantity }}×</strong> {{ item.productName }}<small v-if="item.variationName"> · {{ item.variationName }}</small>
        </span>
      </span>
      <strong>{{ totalLabel }}</strong>
      <span v-if="isNew" class="signal new">Novo pedido</span>
      <span v-if="productionDue" class="signal due">Produzir agora</span>
      <span v-if="isLate" class="signal late">{{ lateLabel }}</span>
    </button>
    <q-btn
      v-if="nextAction"
      class="order-card-action"
      square
      color="primary"
      no-caps
      :icon="nextActionIcon"
      :label="nextAction.label"
      @click="$emit('action', order, nextAction)"
    />
  </article>
</template>

<style scoped>
.order-card-shell {
  margin-bottom: 10px;
  overflow: hidden;
  border: 1px solid var(--zf-border-subtle);
  border-radius: 10px;
  background: var(--zf-surface-raised);
}
.order-card-shell:hover, .order-card-shell.selected { border-color: var(--zf-brand-primary); }
.order-card-shell.late { border-color: var(--zf-status-urgency); }
.order-card-shell.scheduled { background: color-mix(in srgb, var(--zf-status-confirmed) 6%, var(--zf-surface-raised)); }
.order-card-shell.due { border-color: var(--zf-status-warning); }
.order-card {
  display: grid;
  gap: 7px;
  width: 100%;
  padding: 14px;
  border: 0;
  background: transparent;
  color: var(--zf-text-primary);
  text-align: left;
  cursor: pointer;
  min-height: 44px;
}
.order-card:focus-visible { outline-offset: -3px; }
.order-card-title { display: flex; justify-content: space-between; align-items: center; gap: 16px; }
.order-card-items { display: grid; gap: 3px; padding: 8px 0; border-block: 1px solid var(--zf-border-subtle); }
.order-card-items small { color: var(--zf-text-muted); }
.order-card-action { width: calc(100% - 20px); min-height: 44px; margin: 0 10px 10px; }
.signal { width: fit-content; padding: 2px 7px; border-radius: 999px; font-size: .75rem; font-weight: 700; }
.signal.new { background: color-mix(in srgb, var(--zf-status-info) 12%, white); color: var(--zf-status-info); }
.signal.due { background: color-mix(in srgb, var(--zf-status-warning) 14%, var(--zf-surface-raised)); color: var(--zf-status-warning-text); }
.signal.late { background: color-mix(in srgb, var(--zf-status-urgency) 10%, var(--zf-surface-raised)); color: var(--zf-status-urgency); }
</style>
