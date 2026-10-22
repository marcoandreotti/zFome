<script setup lang="ts">
import { computed, onMounted, onUnmounted, reactive, ref } from 'vue'
import ProblemBanner from '../../../components/ProblemBanner.vue'
import { useSessionStore } from '../../session/store'
import { catalogClient } from '../catalog/client'
import {
  availabilityClient,
  serviceTypeLabels,
  type AvailabilityConfiguration,
  type OrderSchedulingConfiguration,
  type OfferKind,
  type ScheduleException,
  type ServiceType
} from './client'

const session = useSessionStore()
const configuration = ref<AvailabilityConfiguration>()
const catalog = ref<Awaited<ReturnType<typeof catalogClient.get>>>()
const scheduling = ref<OrderSchedulingConfiguration>()
const loading = ref(true)
const busy = ref(false)
const error = ref<unknown>(null)
const message = ref('')
const serviceTypes: ServiceType[] = ['Table', 'Pickup', 'Delivery']
const serviceTypeOptions = serviceTypes.map(value => ({ label: serviceTypeLabels[value], value }))
const exception = reactive<ScheduleException>({
  date: '', serviceType: null, isOpen: false,
  opensAt: null, closesAt: null, reason: null
})
const pause = reactive({ serviceType: 'Pickup' as ServiceType, endsAt: '', reason: '' })
const offer = reactive({ key: '', endsAt: '', reason: '' })
let controller: AbortController | undefined

const offerOptions = computed(() => (catalog.value?.categories ?? []).flatMap(category =>
  category.products.flatMap(product => [
    { label: 'Produto · ' + product.name, value: 'Product:' + product.id, available: product.isAvailable },
    ...product.variations.map(variation => ({
      label: 'Variação · ' + product.name + ' / ' + variation.name,
      value: 'Variation:' + variation.id, available: variation.isAvailable
    })),
    ...product.additionalGroups.flatMap(group => group.items.map(item => ({
      label: 'Adicional · ' + item.name, value: 'Additional:' + item.id, available: item.isAvailable
    })))
  ])
))
const selectedOffer = computed(() => offerOptions.value.find(item => item.value === offer.key))

async function load() {
  if (!session.unitId) return
  controller?.abort()
  controller = new AbortController()
  loading.value = true
  error.value = null
  try {
    const [config, tree, scheduleConfig] = await Promise.all([
      availabilityClient.configuration(session.unitId, controller.signal),
      catalogClient.get(session.unitId, controller.signal),
      availabilityClient.scheduling(session.unitId, controller.signal)
    ])
    configuration.value = config
    catalog.value = tree
    scheduling.value = scheduleConfig
  } catch (failure) { error.value = failure }
  finally { loading.value = false }
}
async function execute(action: () => Promise<void>, success: string) {
  busy.value = true; error.value = null; message.value = ''
  try { await action(); message.value = success; await load() }
  catch (failure) { error.value = failure }
  finally { busy.value = false }
}
function addException() {
  if (!configuration.value || !exception.date) return
  configuration.value.exceptions.push({ ...exception })
  Object.assign(exception, { date: '', serviceType: null, isOpen: false, opensAt: null, closesAt: null, reason: null })
}
function offerTarget(): { kind: OfferKind; id: string } {
  const [kind, id] = offer.key.split(':')
  return { kind: kind as OfferKind, id: id ?? '' }
}
onMounted(load)
onUnmounted(() => controller?.abort())
</script>

<template>
  <q-page class="admin-page">
    <header class="admin-page-header"><div><p class="admin-page-eyebrow">OPERAÇÃO</p><h1 class="text-h4 q-my-sm">Disponibilidade</h1>
      <p>Controle fuso, exceções de calendário, pausas por modalidade e ofertas temporariamente indisponíveis.</p></div></header>
    <ProblemBanner :error="error" />
    <q-banner v-if="message" class="bg-green-1 text-positive q-mb-md">{{ message }}</q-banner>
    <div v-if="loading" role="status">Carregando disponibilidade…</div>
    <div v-else-if="configuration" class="q-gutter-lg">
      <q-card flat bordered><q-card-section><h2 class="text-h6">Fuso da unidade</h2>
        <div class="row q-gutter-md items-start"><q-input v-model="configuration.timeZoneId" outlined label="Fuso IANA" hint="Ex.: America/Sao_Paulo" class="col" />
          <q-btn color="primary" icon="save" label="Salvar fuso" :loading="busy" @click="execute(() => availabilityClient.timeZone(session.unitId, configuration!), 'Fuso atualizado.')" /></div>
      </q-card-section></q-card>

      <q-card v-if="scheduling" flat bordered><q-card-section><h2 class="text-h6">Pedidos agendados</h2>
        <p>Os horários são exibidos em {{ scheduling.timeZoneId }}, em intervalos fixos de 30 minutos.</p>
        <div v-for="policy in scheduling.policies" :key="policy.serviceType" class="q-mb-lg">
          <h3 class="text-subtitle1">{{ policy.serviceType === 'Pickup' ? 'Retirada' : 'Entrega' }}</h3>
          <q-toggle v-model="policy.isEnabled" label="Permitir agendamento" />
          <div class="row q-col-gutter-md items-center">
            <q-input v-model.number="policy.minimumAdvanceMinutes" type="number" min="0" max="129600" outlined label="Antecedência mínima (minutos)" class="col-12 col-md-4" />
            <q-input v-model.number="policy.horizonDays" type="number" min="1" max="90" outlined label="Horizonte (dias)" class="col-12 col-md-4" />
            <q-input v-model.number="policy.maximumOrdersPerSlot" type="number" min="1" clearable outlined label="Máx. pedidos por horário (vazio = ilimitado)" class="col-12 col-md-4" />
          </div>
          <q-btn color="primary" icon="save" label="Salvar configuração" :loading="busy" @click="execute(() => availabilityClient.saveScheduling(session.unitId, policy), 'Configuração de agendamento atualizada.')" />
        </div>
      </q-card-section></q-card>

      <q-card flat bordered><q-card-section><h2 class="text-h6">Exceções de calendário</h2>
        <div class="row q-col-gutter-md">
          <q-input v-model="exception.date" type="date" outlined label="Data" class="col-12 col-md-3" />
          <q-select v-model="exception.serviceType" clearable outlined emit-value map-options label="Modalidade (todas se vazio)" :options="serviceTypeOptions" class="col-12 col-md-3" />
          <q-checkbox v-model="exception.isOpen" label="Aberto em horário especial" class="col-12 col-md-3" />
          <q-input v-if="exception.isOpen" v-model="exception.opensAt" type="time" outlined label="Abre" class="col-6 col-md-2" />
          <q-input v-if="exception.isOpen" v-model="exception.closesAt" type="time" outlined label="Fecha" class="col-6 col-md-2" />
          <q-input v-model="exception.reason" outlined label="Motivo" class="col-12 col-md-4" />
        </div>
        <q-btn outline icon="add" label="Adicionar exceção" class="q-mt-md" @click="addException" />
        <q-list bordered separator class="q-mt-md"><q-item v-for="(item,index) in configuration.exceptions" :key="item.date + '-' + (item.serviceType ?? '')">
          <q-item-section>{{ item.date }} · {{ item.serviceType ? serviceTypeLabels[item.serviceType] : 'Todas' }} · {{ item.isOpen ? item.opensAt + '–' + item.closesAt : 'Fechado' }}<small>{{ item.reason }}</small></q-item-section>
          <q-item-section side><q-btn flat square class="collection-action-btn" color="negative" icon="delete" :aria-label="'Remover exceção de ' + item.date" @click="configuration.exceptions.splice(index,1)"><q-tooltip>Remover exceção</q-tooltip></q-btn></q-item-section>
        </q-item></q-list>
        <q-btn color="primary" icon="save" label="Salvar exceções" class="q-mt-md" :loading="busy" @click="execute(() => availabilityClient.exceptions(session.unitId, configuration!.exceptions), 'Exceções atualizadas.')" />
      </q-card-section></q-card>

      <q-card flat bordered><q-card-section><h2 class="text-h6">Pausas por modalidade</h2>
        <q-list v-if="configuration.pauses.length" bordered separator class="q-mb-md"><q-item v-for="item in configuration.pauses" :key="item.serviceType">
          <q-item-section><strong>{{ serviceTypeLabels[item.serviceType] }}</strong><small>{{ item.reason || 'Pausa manual' }} · até {{ item.endsAt ? new Date(item.endsAt).toLocaleString('pt-BR') : 'reativação manual' }}</small></q-item-section>
          <q-item-section side><q-btn flat square class="collection-action-btn" color="positive" icon="play_arrow" :aria-label="'Retomar ' + serviceTypeLabels[item.serviceType]" @click="execute(() => availabilityClient.resume(session.unitId, item.serviceType), 'Modalidade retomada.')"><q-tooltip>Retomar modalidade</q-tooltip></q-btn></q-item-section>
        </q-item></q-list>
        <div class="row q-col-gutter-md"><q-select v-model="pause.serviceType" outlined emit-value map-options label="Modalidade" :options="serviceTypeOptions" class="col-12 col-md-3" />
          <q-input v-model="pause.endsAt" type="datetime-local" outlined label="Até (opcional)" class="col-12 col-md-3" />
          <q-input v-model="pause.reason" outlined label="Motivo" class="col-12 col-md-4" />
          <div class="col-12 col-md-2 pause-submit-cell"><q-btn color="warning" text-color="dark" icon="pause_circle" label="Pausar" class="pause-submit-btn" :loading="busy" @click="execute(() => availabilityClient.pause(session.unitId, pause.serviceType, pause.endsAt || null, pause.reason), 'Modalidade pausada.')" /></div></div>
      </q-card-section></q-card>

      <q-card flat bordered><q-card-section><h2 class="text-h6">Indisponibilidade de oferta</h2>
        <div class="row q-col-gutter-md"><q-select v-model="offer.key" outlined emit-value map-options label="Produto ou opção" :options="offerOptions" class="col-12 col-md-5" />
          <q-input v-model="offer.endsAt" type="datetime-local" outlined label="Até (opcional)" class="col-12 col-md-3" />
          <q-input v-model="offer.reason" outlined label="Motivo" class="col-12 col-md-4" /></div>
        <div class="q-mt-md q-gutter-sm"><q-btn v-if="selectedOffer?.available !== false" color="warning" text-color="dark" icon="block" label="Indisponibilizar" :disable="!offer.key" :loading="busy" @click="execute(() => { const value=offerTarget(); return availabilityClient.unavailable(session.unitId,value.kind,value.id,offer.endsAt || null,offer.reason) }, 'Oferta indisponibilizada.')" />
          <q-btn v-else color="positive" icon="check_circle" label="Reativar oferta" :loading="busy" @click="execute(() => { const value=offerTarget(); return availabilityClient.reactivate(session.unitId,value.kind,value.id) }, 'Oferta reativada.')" /></div>
      </q-card-section></q-card>
    </div>
  </q-page>
</template>
<style scoped>
.pause-submit-cell { display: flex; align-items: flex-start; }
.pause-submit-btn { height: 56px; }
</style>
