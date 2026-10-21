<script setup lang="ts">
import { computed } from 'vue'
import { ApiError } from '../http/client'
const props = defineProps<{ error: unknown }>()
const message = computed(() =>
  props.error instanceof Error
    ? props.error.message
    : 'Não foi possível concluir a operação.'
)
const trace = computed(() =>
  props.error instanceof ApiError ? props.error.problem.traceId : undefined
)
const validation = computed(() =>
  props.error instanceof ApiError
    ? Object.entries(props.error.problem.errors ?? {})
    : []
)
</script>
<template>
  <q-banner
    v-if="error"
    class="bg-red-1 text-negative q-mb-md rounded-borders"
    role="alert"
  >
    {{ message }}
    <ul v-if="validation.length">
      <li v-for="[field, messages] in validation" :key="field">
        {{ field }}: {{ messages.join(' ') }}
      </li>
    </ul>
    <div v-if="trace" class="text-caption">Referência: {{ trace }}</div>
    <slot />
  </q-banner>
</template>
