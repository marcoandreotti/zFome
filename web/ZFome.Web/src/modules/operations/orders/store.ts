import { computed, ref } from 'vue'
import { defineStore } from 'pinia'
import { ApiError } from '../../../http/client'
import { orderOperationsClient, type OrderTransition } from './client'
import type { OrderDetail, OrderFilters, OrderSummary } from './types'

export const useOrderOperationsStore = defineStore('order-operations', () => {
  const unitId = ref('')
  const summaries = ref<Record<string, OrderSummary>>({})
  const ids = ref<string[]>([])
  const details = ref<Record<string, OrderDetail>>({})
  const discoveredAt = ref<Record<string, number>>({})
  const loading = ref(false)
  const selectedId = ref<string | null>(null)
  const lastSuccessAt = ref<number | null>(null)
  const stale = ref(false)
  const error = ref('')
  const conflict = ref('')
  const actionError = ref('')
  let initialized = false

  const orders = computed(() =>
    ids.value.map((id) => summaries.value[id]).filter(Boolean)
  )

  function reset(nextUnit = '') {
    unitId.value = nextUnit
    summaries.value = {}
    ids.value = []
    details.value = {}
    discoveredAt.value = {}
    selectedId.value = null
    lastSuccessAt.value = null
    stale.value = false
    error.value = ''
    conflict.value = ''
    actionError.value = ''
    initialized = false
  }

  async function synchronize(unit: string, filters: OrderFilters) {
    if (!unit) {
      reset()
      return
    }
    if (unitId.value !== unit) reset(unit)
    loading.value = true
    try {
      const page = await orderOperationsClient.search(unit, filters)
      const next = { ...summaries.value }
      const now = Date.now()
      for (const item of page.items) {
        if (initialized && !next[item.id]) discoveredAt.value[item.id] = now
        next[item.id] = item
      }
      summaries.value = next
      ids.value = [...new Set(page.items.map((item) => item.id))]
      initialized = true
      lastSuccessAt.value = now
      stale.value = false
      error.value = ''
    } catch (failure) {
      stale.value = true
      error.value =
        failure instanceof Error
          ? failure.message
          : 'Não foi possível atualizar os pedidos.'
      throw failure
    } finally {
      loading.value = false
    }
  }

  async function select(unit: string, id: string) {
    selectedId.value = id
    actionError.value = ''
    details.value[id] = await orderOperationsClient.detail(unit, id)
  }

  async function transition(
    unit: string,
    id: string,
    value: OrderTransition,
    filters: OrderFilters,
    note?: string
  ) {
    conflict.value = ''
    actionError.value = ''
    try {
      await orderOperationsClient.transition(unit, id, value, note)
      await Promise.all([select(unit, id), synchronize(unit, filters)])
    } catch (failure) {
      if (failure instanceof ApiError && failure.problem.status === 409) {
        conflict.value =
          'Outro operador alterou este pedido. Exibimos abaixo o estado vigente no servidor.'
        await select(unit, id)
      } else {
        actionError.value =
          failure instanceof Error
            ? failure.message
            : 'Não foi possível alterar o pedido.'
      }
      throw failure
    }
  }

  function isNew(id: string, now = Date.now()) {
    return !!discoveredAt.value[id] && now - discoveredAt.value[id] < 120_000
  }

  function isLate(order: OrderSummary, now = Date.now()) {
    if (!['Confirmed', 'Preparing', 'Ready', 'OutForDelivery'].includes(order.status))
      return false
    const threshold = order.status === 'Confirmed' ? 10 : 20
    const dueAt = order.scheduledAtUtc ? Date.parse(order.scheduledAtUtc) : Date.parse(order.createdAt)
    return now - dueAt >= threshold * 60_000
  }

  return {
    unitId,
    summaries,
    ids,
    details,
    orders,
    loading,
    selectedId,
    lastSuccessAt,
    stale,
    error,
    conflict,
    actionError,
    reset,
    synchronize,
    select,
    transition,
    isNew,
    isLate
  }
})
