<script setup lang="ts">
import { onMounted, ref } from 'vue'
import ProblemBanner from '../../../components/ProblemBanner.vue'
import { useSessionStore } from '../../session/store'
import { reviewsClient, type ReviewRow } from './client'

const session = useSessionStore()
const rows = ref<ReviewRow[]>([])
const total = ref(0)
const page = ref(1)
const rating = ref<number | null>(null)
const status = ref('')
const from = ref('')
const to = ref('')
const loading = ref(false)
const error = ref<unknown>(null)
const busy = ref('')
const reasonDialog = ref(false)
const reason = ref('')
const selected = ref<ReviewRow | null>(null)
let requestId = 0
let activeRequest: AbortController | undefined

const dateRangeInvalid = () => !!from.value && !!to.value && from.value > to.value
const dateStart = (value: string) => value ? new Date(`${value}T00:00:00`).toISOString() : undefined
const dateAfter = (value: string) => {
  if (!value) return undefined
  const date = new Date(`${value}T00:00:00`)
  date.setDate(date.getDate() + 1)
  return date.toISOString()
}

const load = async () => {
  if (!session.unitId) return
  if (dateRangeInvalid()) {
    ++requestId
    activeRequest?.abort()
    loading.value = false
    error.value = new Error('A data inicial deve ser anterior ou igual à data final.')
    rows.value = []
    total.value = 0
    return
  }

  const current = ++requestId
  activeRequest?.abort()
  const controller = new AbortController()
  activeRequest = controller
  loading.value = true
  try {
    const result = await reviewsClient.list(session.unitId, {
      rating: rating.value ?? undefined,
      status: status.value || undefined,
      from: dateStart(from.value),
      to: dateAfter(to.value),
      page: page.value,
      pageSize: 20
    }, controller.signal)
    if (current !== requestId) return
    if (result.items.length === 0 && page.value > 1 && (page.value - 1) * 20 >= result.total) {
      page.value--
      await load()
      return
    }
    rows.value = result.items
    total.value = result.total
    error.value = null
  } catch (failure) {
    if (current === requestId && !controller.signal.aborted) error.value = failure
  } finally {
    if (current === requestId) loading.value = false
  }
}

const moderate = async () => {
  if (!session.unitId || !selected.value || !reason.value.trim()) return
  busy.value = selected.value.id
  try {
    if (selected.value.status === 'Pending')
      await reviewsClient.reject(session.unitId, selected.value.id, reason.value.trim())
    else
      await reviewsClient.hide(session.unitId, selected.value.id, reason.value.trim())
    reasonDialog.value = false
    await load()
  } catch (failure) {
    error.value = failure
  } finally {
    busy.value = ''
  }
}

const restore = async (row: ReviewRow) => {
  if (!session.unitId) return
  busy.value = row.id
  try { await reviewsClient.restore(session.unitId, row.id); await load() }
  catch (failure) { error.value = failure }
  finally { busy.value = '' }
}

const approve = async (row: ReviewRow) => {
  if (!session.unitId) return
  busy.value = row.id
  try { await reviewsClient.approve(session.unitId, row.id); await load() }
  catch (failure) { error.value = failure }
  finally { busy.value = '' }
}

const openModeration = (row: ReviewRow) => {
  error.value = null
  selected.value = row
  reason.value = ''
  reasonDialog.value = true
}

onMounted(load)
</script>

<template>
  <q-page class="admin-page review-admin">
    <header class="admin-page-header">
      <div>
        <p class="admin-page-eyebrow">GESTÃO</p>
        <h1 class="text-h4 q-my-sm">Avaliações</h1>
        <p>Comentários públicos e moderação da unidade.</p>
      </div>
    </header>

    <q-banner v-if="!session.unitId" class="bg-amber-1 q-mb-md" rounded>
      Selecione uma unidade para consultar avaliações.
    </q-banner>
    <ProblemBanner :error="error"><q-btn flat label="Tentar novamente" @click="load" /></ProblemBanner>

    <div class="row q-col-gutter-md q-mb-md">
      <q-select v-model="rating" clearable emit-value map-options label="Nota"
        :options="[1, 2, 3, 4, 5].map(value => ({ label: `${value} estrelas`, value }))"
        dense outlined class="col-12 col-sm-3" @update:model-value="page = 1; load()" />
      <q-select v-model="status" clearable label="Situação"
        :options="[
          { label: 'Pendente', value: 'Pending' },
          { label: 'Publicada', value: 'Published' },
          { label: 'Oculta', value: 'Hidden' },
          { label: 'Reprovada', value: 'Rejected' }
        ]" emit-value map-options dense outlined class="col-12 col-sm-3"
        @update:model-value="page = 1; load()" />
      <q-input v-model="from" type="date" label="De" dense outlined class="col-12 col-sm-3"
        @update:model-value="page = 1; load()" />
      <q-input v-model="to" type="date" label="Até" dense outlined class="col-12 col-sm-3"
        @update:model-value="page = 1; load()" />
    </div>

    <q-card flat bordered class="review-admin__table-card">
      <q-table :rows="rows" :columns="[
        { name: 'review', label: 'Avaliação', field: 'rating', align: 'left' },
        { name: 'target', label: 'Alvo', field: 'productName', align: 'left' },
        { name: 'date', label: 'Data', field: 'createdAt', align: 'left' },
        { name: 'status', label: 'Situação', field: 'status', align: 'left' },
        { name: 'actions', label: 'Ações', field: 'id', align: 'right' }
      ]" row-key="id" :loading="loading" :rows-per-page-options="[20]" hide-pagination>
        <template #body-cell-review="props">
          <q-td :props="props">
            <q-rating :model-value="props.row.rating" readonly :max="5" size="1.3em" color="amber"
              :icon-aria-label="['1 estrela', '2 estrelas', '3 estrelas', '4 estrelas', '5 estrelas']"
              :aria-label="`Nota: ${props.row.rating} de 5 estrelas`" />
            <span class="text-caption">{{ props.row.rating }} de 5</span>
            <p v-if="props.row.comment" class="review-admin__comment q-mb-none text-body2">{{ props.row.comment }}</p>
          </q-td>
        </template>
        <template #body-cell-target="props">
          <q-td :props="props">{{ props.row.productName || 'Estabelecimento' }}</q-td>
        </template>
        <template #body-cell-date="props">
          <q-td :props="props">{{ new Date(props.row.createdAt).toLocaleDateString('pt-BR') }}</q-td>
        </template>
        <template #body-cell-status="props">
          <q-td :props="props">
            <q-badge :color="props.row.status === 'Published' ? 'positive' : props.row.status === 'Pending' ? 'orange' : props.row.status === 'Rejected' ? 'negative' : 'grey'">
              {{ props.row.status === 'Pending' ? 'Pendente' : props.row.status === 'Published' ? 'Publicada' : props.row.status === 'Rejected' ? 'Reprovada' : 'Oculta' }}
            </q-badge>
            <div v-if="props.row.moderationReason" class="text-caption">{{ props.row.moderationReason }}</div>
          </q-td>
        </template>
        <template #body-cell-actions="props">
          <q-td :props="props">
            <div class="review-admin__actions">
              <q-btn v-if="props.row.status === 'Pending'" flat color="positive" label="Aprovar e publicar"
                :loading="busy === props.row.id" @click="approve(props.row)" />
              <q-btn v-if="props.row.status === 'Pending' || props.row.status === 'Published'" flat color="negative"
                :label="props.row.status === 'Pending' ? 'Reprovar' : 'Ocultar'"
                :loading="busy === props.row.id" @click="openModeration(props.row)" />
              <q-btn v-if="props.row.status === 'Hidden'" flat color="primary" label="Restaurar e publicar"
                :loading="busy === props.row.id" @click="restore(props.row)" />
            </div>
          </q-td>
        </template>
        <template #no-data>
          <div class="full-width row flex-center q-pa-lg text-grey-7">Nenhuma avaliação encontrada.</div>
        </template>
      </q-table>
      <div class="review-admin__pagination row justify-end q-pa-sm">
        <q-pagination v-model="page" :max="Math.max(1, Math.ceil(total / 20))" @update:model-value="load" />
      </div>
    </q-card>

    <q-dialog v-model="reasonDialog">
      <q-card role="dialog" aria-labelledby="review-moderation-title" style="min-width: min(420px, 90vw)">
        <q-card-section>
          <div id="review-moderation-title" class="text-h6">
            {{ selected?.status === 'Pending' ? 'Reprovar avaliação' : 'Ocultar avaliação' }}
          </div>
          <p>Informe o motivo; ele ficará registrado no histórico de moderação.</p>
          <ProblemBanner :error="error" />
          <q-input v-model="reason" type="textarea" label="Motivo da moderação" outlined
            maxlength="500" counter autofocus />
        </q-card-section>
        <q-card-actions align="right">
          <q-btn flat label="Cancelar" v-close-popup />
          <q-btn color="negative" :label="selected?.status === 'Pending' ? 'Reprovar' : 'Ocultar'"
            :disable="!reason.trim()" :loading="!!busy" @click="moderate" />
        </q-card-actions>
      </q-card>
    </q-dialog>
  </q-page>
</template>

<style scoped>
.review-admin {
  min-width: 0;
}

.review-admin__table-card {
  min-width: 0;
  overflow: hidden;
}

.review-admin__table-card :deep(.q-table__middle) {
  min-width: 0;
  overflow-x: auto;
}

.review-admin__table-card :deep(.q-table) {
  min-width: 760px;
}

.review-admin__table-card :deep(.q-table th),
.review-admin__table-card :deep(.q-table td) {
  white-space: normal;
}

.review-admin__comment {
  max-width: 34rem;
  overflow-wrap: anywhere;
}

.review-admin__actions {
  display: flex;
  flex-wrap: wrap;
  justify-content: flex-end;
  gap: 4px;
}

.review-admin__pagination {
  flex-wrap: wrap;
  gap: 8px;
}

@media (max-width: 600px) {
  .review-admin__table-card :deep(.q-table) {
    min-width: 700px;
  }

  .review-admin__actions {
    justify-content: flex-start;
  }
}
</style>
