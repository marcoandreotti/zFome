<script setup lang="ts">
import { onBeforeUnmount, onMounted, reactive, ref } from 'vue'
import ProblemBanner from '../../../components/ProblemBanner.vue'
import { useSessionStore } from '../../session/store'
import { deliveryClient, type DeliveryRegion, type DeliveryRegionInput } from './client'

const session = useSessionStore()
const regions = ref<DeliveryRegion[]>([])
const loading = ref(true)
const busy = ref(false)
const error = ref<unknown>(null)
const saved = ref(false)
const editingId = ref<string | null>(null)
const form = reactive<DeliveryRegionInput>({ name: '', postalCodeFrom: '', postalCodeTo: '', fee: 0, estimatedMinutes: 40 })
let controller: AbortController | undefined

async function refresh() {
  controller?.abort()
  controller = new AbortController()
  loading.value = true
  try { regions.value = await deliveryClient.list(session.unitId, controller.signal); error.value = null }
  catch (failure) { if (!controller.signal.aborted) error.value = failure }
  finally { if (!controller.signal.aborted) loading.value = false }
}
function edit(region?: DeliveryRegion) {
  editingId.value = region?.id ?? null
  Object.assign(form, region ? {
    name: region.name, postalCodeFrom: region.postalCodeFrom, postalCodeTo: region.postalCodeTo,
    fee: region.fee, estimatedMinutes: region.estimatedMinutes
  } : { name: '', postalCodeFrom: '', postalCodeTo: '', fee: 0, estimatedMinutes: 40 })
}
async function save() {
  busy.value = true; saved.value = false; error.value = null
  try { await deliveryClient.save(session.unitId, editingId.value, { ...form, fee: Number(form.fee), estimatedMinutes: Number(form.estimatedMinutes) }); saved.value = true; edit(); await refresh() }
  catch (failure) { error.value = failure }
  finally { busy.value = false }
}
async function toggle(region: DeliveryRegion) {
  busy.value = true; error.value = null
  try { await deliveryClient.active(session.unitId, region.id, !region.isActive); await refresh() }
  catch (failure) { error.value = failure }
  finally { busy.value = false }
}
onMounted(() => void refresh())
onBeforeUnmount(() => controller?.abort())
</script>

<template>
  <q-page class="admin-page">
    <header class="admin-page-header">
      <div>
        <p class="admin-page-eyebrow">ENTREGAS</p>
        <h1 class="text-h4 q-my-sm">Regiões de entrega</h1>
        <p>Configure faixas de CEP, tarifa e prazo estimado. A cobertura é validada no servidor.</p>
      </div>
    </header>
    <ProblemBanner :error="error"><q-btn flat label="Tentar novamente" @click="refresh" /></ProblemBanner>
    <q-banner v-if="saved" class="bg-green-1 text-positive q-mb-md" rounded>Região salva.</q-banner>
    <div class="row q-col-gutter-lg">
      <section class="col-12 col-lg-5" aria-label="Formulário de região">
        <q-card flat bordered><q-card-section>
          <h2 class="text-h6 q-mt-none">{{ editingId ? 'Editar região' : 'Adicionar região' }}</h2>
          <q-form class="q-gutter-md" @submit.prevent="save">
            <q-input v-model="form.name" label="Nome da região" maxlength="100" required />
            <div class="row q-col-gutter-sm">
              <q-input class="col" v-model="form.postalCodeFrom" label="CEP inicial" mask="#####-###" required />
              <q-input class="col" v-model="form.postalCodeTo" label="CEP final" mask="#####-###" required />
            </div>
            <div class="row q-col-gutter-sm">
              <q-input class="col" v-model.number="form.fee" label="Taxa (R$)" type="number" min="0" step="0.01" required />
              <q-input class="col" v-model.number="form.estimatedMinutes" label="Prazo (min)" type="number" min="1" max="1440" required />
            </div>
            <div class="q-gutter-sm"><q-btn color="primary" :icon="editingId ? 'save' : 'add'" :loading="busy" type="submit" :label="editingId ? 'Salvar alterações' : 'Adicionar região'" />
              <q-btn v-if="editingId" flat label="Cancelar edição" @click="edit()" /></div>
          </q-form>
        </q-card-section></q-card>
      </section>
      <section class="col-12 col-lg-7" aria-label="Regiões configuradas">
        <q-card flat bordered><q-card-section>
          <div class="row items-center justify-between"><h2 class="text-h6 q-my-none">Coberturas</h2><q-btn flat label="Atualizar" :loading="loading" @click="refresh" /></div>
          <div v-if="loading" role="status" class="q-pa-md">Carregando regiões…</div>
          <q-list v-else-if="regions.length" separator class="q-mt-md">
            <q-item v-for="region in regions" :key="region.id" class="q-px-none">
              <q-item-section><q-item-label>{{ region.name }} · {{ region.isActive ? 'Ativa' : 'Inativa' }}</q-item-label>
                <q-item-label caption>CEP {{ region.postalCodeFrom }}–{{ region.postalCodeTo }} · R$ {{ region.fee.toFixed(2) }} · {{ region.estimatedMinutes }} min</q-item-label></q-item-section>
              <q-item-section side><div class="row q-gutter-xs"><q-btn flat square class="collection-action-btn" color="primary" icon="edit" :aria-label="`Editar região ${region.name}`" @click="edit(region)"><q-tooltip>Editar {{ region.name }}</q-tooltip></q-btn>
                <q-btn flat square class="collection-action-btn" :color="region.isActive ? 'negative' : 'positive'" :icon="region.isActive ? 'toggle_off' : 'toggle_on'" :aria-label="`${region.isActive ? 'Desativar' : 'Ativar'} região ${region.name}`" :loading="busy" @click="toggle(region)"><q-tooltip>{{ region.isActive ? 'Desativar' : 'Ativar' }} {{ region.name }}</q-tooltip></q-btn></div></q-item-section>
            </q-item>
          </q-list>
          <p v-else class="text-grey-7 q-mt-md">Nenhuma região configurada. Entregas ficarão indisponíveis até ativar uma cobertura.</p>
        </q-card-section></q-card>
      </section>
    </div>
  </q-page>
</template>
