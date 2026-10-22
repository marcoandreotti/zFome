import { computed, ref } from 'vue'
import { defineStore } from 'pinia'
import { ApiError } from '../../../http/client'
import { kitchenDisplayClient } from './client'
import type { KitchenTicket } from './types'

export const useKitchenDisplayStore = defineStore('kitchen-display', () => {
  const unitId = ref('')
  const tickets = ref<KitchenTicket[]>([])
  const loading = ref(false)
  const stale = ref(false)
  const lastSuccessAt = ref<number | null>(null)
  const error = ref('')
  const conflict = ref('')
  const actionError = ref('')
  const busyId = ref<string | null>(null)

  const waiting = computed(() =>
    tickets.value.filter((ticket) => ticket.status === 'Confirmed')
  )
  const preparing = computed(() =>
    tickets.value.filter((ticket) => ticket.status === 'Preparing')
  )

  function reset(nextUnit = '') {
    unitId.value = nextUnit
    tickets.value = []
    loading.value = false
    stale.value = false
    lastSuccessAt.value = null
    error.value = ''
    conflict.value = ''
    actionError.value = ''
    busyId.value = null
  }

  async function synchronize(unit: string) {
    if (!unit) {
      reset()
      return
    }
    if (unitId.value !== unit) reset(unit)
    loading.value = true
    try {
      const snapshot = await kitchenDisplayClient.queue(unit)
      if (unitId.value !== unit) return
      tickets.value = snapshot
      lastSuccessAt.value = Date.now()
      stale.value = false
      error.value = ''
    } catch (failure) {
      if (unitId.value !== unit) return
      stale.value = true
      error.value =
        failure instanceof Error
          ? failure.message
          : 'Não foi possível atualizar a fila da cozinha.'
      throw failure
    } finally {
      if (unitId.value === unit) loading.value = false
    }
  }

  async function execute(unit: string, ticket: KitchenTicket) {
    conflict.value = ''
    actionError.value = ''
    busyId.value = ticket.id
    try {
      await kitchenDisplayClient.execute(unit, ticket.id, ticket.action)
      await synchronize(unit)
    } catch (failure) {
      if (failure instanceof ApiError && failure.problem.status === 409) {
        conflict.value =
          'Outro terminal alterou este pedido. A fila foi reconciliada com o estado atual do servidor.'
        await synchronize(unit)
      } else {
        actionError.value =
          failure instanceof Error
            ? failure.message
            : 'Não foi possível avançar o pedido.'
      }
      throw failure
    } finally {
      busyId.value = null
    }
  }

  return {
    unitId,
    tickets,
    waiting,
    preparing,
    loading,
    stale,
    lastSuccessAt,
    error,
    conflict,
    actionError,
    busyId,
    reset,
    synchronize,
    execute
  }
})
