<script setup lang="ts">
import { onMounted, onUnmounted, ref, computed } from 'vue'
import { isCancel } from 'axios'
import ProblemBanner from '../../../components/ProblemBanner.vue'
import {
  catalogClient,
  type ReusableItem,
  type ReusableResource
} from './client'
const props = defineProps<{
  unitId: string
  resource: ReusableResource
  excludedIds: string[]
}>()
const emit = defineEmits<{ selected: [item: ReusableItem]; close: [] }>()
const search = ref(''),
  page = ref(1),
  total = ref(0),
  loading = ref(false),
  error = ref<unknown>(null)
const items = ref<ReusableItem[]>([])
const maxPage = computed(() => Math.max(1, Math.ceil(total.value / 20)))
let request: AbortController | undefined
async function load() {
  request?.abort()
  const current = new AbortController()
  request = current
  loading.value = true
  error.value = null
  try {
    const result = await catalogClient.search(
      props.unitId,
      props.resource,
      { search: search.value, page: page.value, pageSize: 20 },
      current.signal
    )
    if (!current.signal.aborted) {
      items.value = result.items
      total.value = result.total
    }
  } catch (failure) {
    if (!isCancel(failure)) {
      error.value = failure
      items.value = []
      total.value = 0
    }
  } finally {
    if (request === current) loading.value = false
  }
}
function filter() {
  page.value = 1
  void load()
}
onMounted(load)
onUnmounted(() => request?.abort())
</script>
<template>
  <q-card style="width: 580px; max-width: 95vw"
    ><q-card-section>
      <h2 class="text-h6">
        Selecionar {{ resource === 'additionals' ? 'adicional' : 'grupo' }}
      </h2>
      <ProblemBanner :error="error"
        ><q-btn flat label="Tentar novamente" @click="load"
      /></ProblemBanner>
      <q-form class="row q-gutter-sm" @submit="filter"
        ><q-input
          v-model="search"
          autofocus
          outlined
          label="Pesquisar por nome"
          maxlength="150"
          class="col" /><q-btn
          type="submit"
          label="Pesquisar"
          :loading="loading"
      /></q-form>
      <q-list bordered class="q-mt-md"
        ><q-item v-for="item in items" :key="item.id"
          ><q-item-section
            >{{ item.name }}
            <span v-if="!item.isActive" class="text-caption"
              >Inativo</span
            ></q-item-section
          ><q-item-section side
            ><q-btn flat
              square
              class="collection-action-btn"
              color="primary"
              icon="add"
              :aria-label="`Selecionar ${item.name}`"
              :disable="loading || excludedIds.includes(item.id)"
              @click="emit('selected', item)"
              ><q-tooltip>Selecionar {{ item.name }}</q-tooltip></q-btn
            ></q-item-section></q-item
      ></q-list>
      <p v-if="loading" role="status">Carregando…</p>
      <p v-else-if="!items.length && !error" role="status">Nenhum resultado.</p>
      <q-pagination
        v-model="page"
        :max="maxPage"
        :max-pages="5"
        :disable="loading"
        @update:model-value="load"
        class="q-mt-md"
      /> </q-card-section
    ><q-card-actions align="right"
      ><q-btn
        flat
        label="Fechar seleção"
        @click="emit('close')" /></q-card-actions
  ></q-card>
</template>
