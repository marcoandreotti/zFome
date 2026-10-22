<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref, watch } from 'vue'
import { isCancel } from 'axios'
import ProblemBanner from '../../../components/ProblemBanner.vue'
import { useSessionStore } from '../../session/store'
import {
  communicationsClient,
  type NotificationAttempt,
  type NotificationChannel,
  type NotificationConsent,
  type NotificationHistory,
  type NotificationTemplate,
  type NotificationTemplateInput
} from './client'

const session = useSessionStore()
const templates = ref<NotificationTemplate[]>([])
const consents = ref<NotificationConsent[]>([])
const history = ref<NotificationHistory[]>([])
const attemptHistory = ref<NotificationAttempt[]>([])
const attemptDialog = ref(false)
const attemptLoading = ref(false)
const explanationDialog = ref(false)
const helpDialog = ref(false)
const templateDialog = ref(false)
const sendDialog = ref(false)
const consentHistoryDialog = ref(false)
const revokeDialog = ref(false)
const selectedConsent = ref<NotificationConsent | null>(null)
const revocationSource = ref('')
const consentHistory = ref<NotificationConsent[]>([])
const currentTab = ref('templates')
const periodStart = ref(localDate(new Date(Date.now() - 29 * 86400000)))
const periodEnd = ref(localDate(new Date()))
const selectedTemplateId = ref('')
const loading = ref(false)
const busy = ref(false)
const error = ref<unknown>(null)
const message = ref('')
const channelOptions = [
  { label: 'E-mail', value: 'Email' as NotificationChannel },
  { label: 'WhatsApp', value: 'WhatsApp' as NotificationChannel }
]
const historyColumns = [
  { name: 'createdAtUtc', label: 'Data', field: 'createdAtUtc', align: 'left' as const, format: (value: string) => new Date(value).toLocaleString('pt-BR') },
  { name: 'channel', label: 'Canal', field: 'channel', align: 'left' as const, format: (value: string) => translateChannel(value) },
  { name: 'purpose', label: 'Finalidade', field: 'purpose', align: 'left' as const },
  { name: 'destination', label: 'Destino', field: 'destination', align: 'left' as const },
  { name: 'status', label: 'Estado', field: 'status', align: 'left' as const, format: (value: string) => translateStatus(value) },
  { name: 'attemptCount', label: 'Tentativas', field: 'attemptCount', align: 'right' as const },
  { name: 'lastError', label: 'Código de erro', field: 'lastError', align: 'left' as const },
  { name: 'details', label: 'Histórico', field: 'id', align: 'left' as const }
]
const template = ref<NotificationTemplateInput>(blankTemplate())
const consent = ref({
  channel: 'Email' as NotificationChannel,
  purpose: 'order.update',
  destination: '',
  isGranted: true,
  source: ''
})
const send = ref({ destination: '', parametersJson: '{}', idempotencyKey: crypto.randomUUID() })
const selectedTemplate = computed(
  () => templates.value.find((item) => item.id === selectedTemplateId.value) ?? null
)
const latestConsents = computed(() => {
  const grouped = new Map<string, NotificationConsent[]>()
  for (const item of consents.value) {
    const key = `${item.channel}|${item.purpose.trim().toLowerCase()}|${normalizeDestination(item.channel, item.destination)}`
    grouped.set(key, [...(grouped.get(key) ?? []), item])
  }
  return [...grouped.entries()].map(([key, events]) => ({ key, current: events[0]!, events }))
})
let request: AbortController | undefined

function blankTemplate(): NotificationTemplateInput {
  return {
    id: null,
    purpose: 'order.update',
    channel: 'Email',
    language: 'pt_BR',
    subject: '',
    body: '',
    providerTemplateName: null,
    requiresConsent: true,
    isActive: true
  }
}

function localDate(date: Date) {
  const year = date.getFullYear()
  const month = String(date.getMonth() + 1).padStart(2, '0')
  const day = String(date.getDate()).padStart(2, '0')
  return `${year}-${month}-${day}`
}

function normalizeDestination(channel: NotificationChannel, destination: string) {
  return channel === 'Email' ? destination.trim().toLowerCase() : destination.replace(/\D/g, '')
}

function dateBounds() {
  const from = new Date(`${periodStart.value}T00:00:00`)
  const to = new Date(`${periodEnd.value}T00:00:00`)
  to.setDate(to.getDate() + 1)
  return { fromUtc: from.toISOString(), toUtcExclusive: to.toISOString() }
}

async function reloadHistory() {
  if (!session.unitId) return
  loading.value = true
  error.value = null
  try {
    history.value = await communicationsClient.history(session.unitId, dateBounds())
  } catch (failure) {
    error.value = failure
  } finally {
    loading.value = false
  }
}

function selectLastThirtyDays() {
  periodStart.value = localDate(new Date(Date.now() - 29 * 86400000))
  periodEnd.value = localDate(new Date())
  void reloadHistory()
}

function translateStatus(status: string) {
  return ({
    Queued: 'Na fila',
    AcceptedByProvider: 'Aceito pelo provedor',
    BlockedByConsent: 'Bloqueado por consentimento',
    RetryScheduled: 'Nova tentativa agendada',
    Failed: 'Falhou',
    Uncertain: 'Resultado incerto'
  } as Record<string, string>)[status] ?? status
}

function translateChannel(channel: string) {
  return channel === 'Email' ? 'E-mail' : channel
}

function paginationLabel(first: number, last: number, total: number) {
  return `${first}–${last} de ${total}`
}

async function load() {
  if (!session.unitId) return
  request?.abort()
  const current = new AbortController()
  request = current
  loading.value = true
  error.value = null
  try {
    const [loadedTemplates, loadedConsents, loadedHistory] = await Promise.all([
      communicationsClient.templates(session.unitId, current.signal),
      communicationsClient.consents(session.unitId, current.signal),
      communicationsClient.history(session.unitId, { ...dateBounds(), signal: current.signal })
    ])
    if (current.signal.aborted) return
    templates.value = loadedTemplates
    consents.value = loadedConsents
    history.value = loadedHistory
    if (!loadedTemplates.some((item) => item.id === selectedTemplateId.value))
      selectedTemplateId.value = loadedTemplates[0]?.id ?? ''
  } catch (failure) {
    if (!isCancel(failure)) error.value = failure
  } finally {
    if (request === current) loading.value = false
  }
}

function edit(row: NotificationTemplate) {
  template.value = { ...row }
  selectedTemplateId.value = row.id
  message.value = ''
  templateDialog.value = true
}

function create() {
  template.value = blankTemplate()
  message.value = ''
  templateDialog.value = true
}

async function saveTemplate() {
  if (busy.value || !session.unitId) return
  busy.value = true
  error.value = null
  try {
    const result = await communicationsClient.saveTemplate(session.unitId, template.value)
    message.value = 'Modelo salvo.'
    await load()
    selectedTemplateId.value = result.id
    const stored = templates.value.find((item) => item.id === result.id)
    if (stored) selectedTemplateId.value = stored.id
    templateDialog.value = false
  } catch (failure) {
    error.value = failure
  } finally {
    busy.value = false
  }
}

async function saveConsent() {
  if (busy.value || !session.unitId) return
  busy.value = true
  error.value = null
  try {
    await communicationsClient.saveConsent(session.unitId, consent.value)
    message.value = consent.value.isGranted ? 'Consentimento registrado.' : 'Revogação do consentimento registrada.'
    consents.value = await communicationsClient.consents(session.unitId)
  } catch (failure) {
    error.value = failure
  } finally {
    busy.value = false
  }
}

async function requestNotification() {
  if (busy.value || !session.unitId || !selectedTemplate.value) return
  busy.value = true
  error.value = null
  try {
    const parameters = JSON.parse(send.value.parametersJson) as Record<string, string>
    await communicationsClient.request(session.unitId, {
      templateId: selectedTemplate.value.id,
      channel: selectedTemplate.value.channel,
      destination: send.value.destination,
      parameters,
      idempotencyKey: send.value.idempotencyKey
    })
    message.value = 'Solicitação incluída na fila de envio.'
    send.value.idempotencyKey = crypto.randomUUID()
    await reloadHistory()
    sendDialog.value = false
  } catch (failure) {
    error.value = failure
  } finally {
    busy.value = false
  }
}

function showConsentEvents(events: NotificationConsent[]) {
  consentHistory.value = events
  consentHistoryDialog.value = true
}

function revokeConsent(item: NotificationConsent) {
  selectedConsent.value = item
  revocationSource.value = ''
  revokeDialog.value = true
}

async function confirmRevocation() {
  if (busy.value || !session.unitId || !selectedConsent.value || !revocationSource.value.trim()) return
  busy.value = true
  error.value = null
  try {
    await communicationsClient.saveConsent(session.unitId, {
      channel: selectedConsent.value.channel,
      purpose: selectedConsent.value.purpose,
      destination: selectedConsent.value.destination,
      isGranted: false,
      source: revocationSource.value.trim()
    })
    consents.value = await communicationsClient.consents(session.unitId)
    message.value = 'Revogação registrada. Os registros anteriores foram preservados.'
    revokeDialog.value = false
  } catch (failure) {
    error.value = failure
  } finally {
    busy.value = false
  }
}

async function showAttempts(notificationId: string) {
  if (!session.unitId) return
  attemptDialog.value = true
  attemptLoading.value = true
  try {
    attemptHistory.value = await communicationsClient.attempts(session.unitId, notificationId)
  } catch (failure) {
    error.value = failure
    attemptDialog.value = false
  } finally {
    attemptLoading.value = false
  }
}

watch(() => session.unitId, () => void load())
watch(() => template.value.channel, (channel) => {
  if (channel === 'WhatsApp') template.value.subject = ''
})
onMounted(() => void load())
onUnmounted(() => request?.abort())
</script>

<template>
  <q-page class="admin-page communications-page">
    <header class="admin-page-header">
      <div>
        <h1 class="text-h4 q-my-sm">Notificações</h1>
        <p class="header-description">Prepare mensagens, registre permissões e acompanhe as solicitações de envio da unidade.
          <q-btn flat round dense size="sm" icon="info_outline" aria-label="Como as notificações funcionam" @click="explanationDialog = true"><q-tooltip>Como as notificações funcionam</q-tooltip></q-btn>
        </p>
      </div>
      <q-btn outline icon="send" label="Solicitar envio" :disable="!session.unitId || !templates.some((item) => item.isActive)" @click="sendDialog = true" />
    </header>
    <q-banner v-if="!session.unitId" class="bg-amber-1">Selecione uma unidade autorizada.</q-banner>
    <p v-if="message" role="status" class="text-positive">{{ message }}</p>
    <ProblemBanner :error="error"><q-btn flat label="Tentar novamente" @click="load" /></ProblemBanner>

    <div v-if="session.unitId" class="notification-workflow">
      <q-tabs v-model="currentTab" dense align="left" active-color="primary" indicator-color="primary" aria-label="Seções de notificações">
        <q-tab name="templates" label="Modelos da unidade" />
        <q-tab name="consents" label="Consentimentos" />
        <q-tab name="history" label="Histórico de envios" />
      </q-tabs>
      <q-tab-panels v-model="currentTab" animated>
      <q-tab-panel name="templates" class="notification-tab-panel">
        <div class="notification-section-heading">
          <div><h2 id="template-heading" class="text-h6 q-mb-xs">Modelos da unidade</h2><p>Configure as mensagens usadas nas solicitações desta unidade.</p></div>
          <q-btn color="primary" icon="add" label="Novo modelo" :disable="!session.unitId" @click="create" />
        </div>
        <q-table :rows="templates" row-key="id" :loading="loading" :pagination="{ rowsPerPage: 10 }" rows-per-page-label="Registros por página" :pagination-label="paginationLabel" :columns="[
          { name: 'purpose', label: 'Finalidade', field: 'purpose', align: 'left' },
          { name: 'channel', label: 'Canal', field: 'channel', align: 'left', format: (value: string) => translateChannel(value) },
          { name: 'language', label: 'Idioma', field: 'language', align: 'left' },
          { name: 'consent', label: 'Consentimento', field: 'requiresConsent', align: 'left', format: (value: boolean) => value ? 'Exigido' : 'Não exigido' },
          { name: 'active', label: 'Estado', field: 'isActive', align: 'left', format: (value: boolean) => value ? 'Ativo' : 'Inativo' },
          { name: 'actions', label: 'Ações', field: 'id', align: 'right' }
        ]" no-data-label="Nenhum modelo cadastrado para esta unidade.">
          <template #body-cell-actions="props"><q-td :props="props"><q-btn flat square class="collection-action-btn" icon="edit" aria-label="Editar modelo" @click="edit(props.row)"><q-tooltip>Editar modelo</q-tooltip></q-btn></q-td></template>
          <template #no-data><div class="notification-empty-state"><strong>Nenhum modelo cadastrado</strong><p>Crie um modelo para definir o conteúdo e o canal das mensagens desta unidade.</p><q-btn outline icon="add" label="Criar primeiro modelo" @click="create" /></div></template>
        </q-table>
      </q-tab-panel>

      <q-tab-panel name="consents" class="notification-tab-panel">
        <div class="notification-section-heading"><div><h2 id="consent-heading" class="text-h6 q-mb-xs">Consentimentos</h2><p>Uma autorização vale para uma pessoa, um canal e uma finalidade. Revogações ficam registradas no histórico.</p></div></div>
        <div class="notification-consent-layout">
          <section aria-labelledby="consent-form-heading">
            <h3 id="consent-form-heading" class="text-subtitle1">Registrar autorização</h3>
            <q-form class="consent-form-grid" @submit.prevent="saveConsent">
              <q-select v-model="consent.channel" outlined emit-value map-options label="Canal *" :options="channelOptions" />
              <q-input v-model="consent.purpose" outlined label="Finalidade *" maxlength="100" required hint="Deve corresponder à finalidade do modelo." />
              <q-input v-model="consent.destination" class="consent-form-wide" outlined label="E-mail ou telefone *" maxlength="254" required hint="Destino que deu a autorização." />
              <q-input v-model="consent.source" class="consent-form-wide" outlined label="Origem da autorização *" maxlength="200" required hint="Ex.: formulário, ligação ou atendimento presencial." />
              <div class="consent-form-wide"><p class="text-caption text-grey-7 q-mb-sm">Uma nova autorização atualiza o estado atual e mantém os eventos anteriores no histórico.</p><q-btn type="submit" outline icon="verified_user" label="Registrar autorização" :loading="busy" /></div>
            </q-form>
          </section>
          <section class="col-12 col-lg-7" aria-labelledby="consent-list-heading">
            <h3 id="consent-list-heading" class="text-subtitle1">Autorizações por destino</h3>
            <q-list v-if="latestConsents.length" bordered separator class="rounded-borders">
              <q-item v-for="group in latestConsents" :key="group.key">
                <q-item-section>
                  <q-item-label>{{ group.current.destination }} · {{ translateChannel(group.current.channel) }} · {{ group.current.purpose }}</q-item-label>
                  <q-item-label caption>{{ group.current.isGranted ? 'Autorização ativa' : 'Revogada' }} · Atualizada em {{ new Date(group.current.capturedAtUtc).toLocaleString('pt-BR') }} · {{ group.current.source || 'Origem não informada' }}</q-item-label>
                </q-item-section>
                <q-item-section side class="row items-center no-wrap"><q-btn flat label="Ver histórico" @click="showConsentEvents(group.events)" /><q-btn v-if="group.current.isGranted" flat color="negative" label="Revogar" @click="revokeConsent(group.current)" /></q-item-section>
              </q-item>
            </q-list>
            <p v-else class="text-grey-7">Nenhuma autorização registrada para esta unidade.</p>
          </section>
        </div>
      </q-tab-panel>

      <q-tab-panel name="history" class="notification-tab-panel">
        <div class="notification-section-heading"><div><h2 id="history-heading" class="text-h6 q-mb-xs">Histórico de envios</h2><p>Consulte o estado das solicitações e as tentativas registradas pelo provedor. O aceite não confirma a entrega.</p></div><q-btn color="primary" icon="send" label="Solicitar envio" :disable="!templates.some((item) => item.isActive)" @click="sendDialog = true" /></div>
        <div class="row q-col-gutter-md items-end q-mb-md"><q-input v-model="periodStart" class="col-12 col-sm-4" outlined type="date" label="De" /><q-input v-model="periodEnd" class="col-12 col-sm-4" outlined type="date" label="Até" /><div class="col-12 col-sm-auto row q-gutter-sm"><q-btn outline label="Últimos 30 dias" :loading="loading" @click="selectLastThirtyDays" /><q-btn outline label="Aplicar período" :disable="!periodStart || !periodEnd || periodStart > periodEnd" :loading="loading" @click="reloadHistory" /></div></div>
        <p class="text-caption text-grey-7">Mostrando até 100 solicitações no período selecionado.</p>
        <q-table :rows="history" row-key="id" :loading="loading" :pagination="{ rowsPerPage: 10 }" rows-per-page-label="Registros por página" :pagination-label="paginationLabel" :columns="historyColumns" no-data-label="Não há solicitações nesse período.">
          <template #body-cell-details="props"><q-td :props="props"><q-btn flat label="Ver tentativas" :disable="!props.row.attemptCount" @click="showAttempts(props.row.id)" /></q-td></template>
        </q-table>
      </q-tab-panel>
      </q-tab-panels>

      <q-dialog v-model="templateDialog" aria-labelledby="template-dialog-title">
        <q-card class="notification-dialog-card"><q-card-section class="row items-center"><h2 id="template-dialog-title" class="text-h6 q-my-none">{{ template.id ? 'Editar modelo' : 'Novo modelo' }}</h2><q-btn flat round dense icon="help_outline" aria-label="Ajuda: finalidades e campos disponíveis" @click="helpDialog = true"><q-tooltip>Ajuda: finalidades e campos disponíveis</q-tooltip></q-btn><q-space /><q-btn flat round dense icon="close" aria-label="Fechar formulário" v-close-popup /></q-card-section><q-card-section class="notification-template-content">
            <q-form class="notification-template-form" @submit.prevent="saveTemplate">
              <q-input v-model="template.purpose" outlined label="Finalidade *" maxlength="100" required hint="Identifica quando o modelo será usado. Ex.: order.update" />
              <div class="notification-template-row">
                <q-select v-model="template.channel" outlined emit-value map-options label="Canal *" :options="channelOptions" />
                <q-input v-model="template.language" outlined label="Idioma do modelo *" maxlength="16" required hint="Use o código do idioma, por exemplo pt_BR." />
              </div>
              <q-input v-if="template.channel === 'Email'" v-model="template.subject" outlined label="Assunto do e-mail *" maxlength="250" required />
              <q-input v-if="template.channel === 'WhatsApp'" v-model="template.providerTemplateName" outlined label="Nome do modelo aprovado na Meta *" maxlength="200" required hint="Use o nome exato do modelo aprovado na sua conta Meta." />
              <q-input
                v-model="template.body"
                outlined
                type="textarea"
                :label="template.channel === 'Email' ? 'Conteúdo do e-mail *' : 'Conteúdo aprovado do modelo WhatsApp *'"
                maxlength="10000"
                required
                :hint="template.channel === 'Email' ? 'Personalize a mensagem com os campos listados na ajuda.' : 'Use os campos numéricos definidos no modelo aprovado, como {{1}} e {{2}}.'"
              />
              <div class="notification-toggle-group">
                <q-toggle v-model="template.requiresConsent" label="Exigir consentimento antes do envio" />
                <q-toggle v-model="template.isActive" label="Modelo ativo" />
              </div>
              <div class="row justify-end q-gutter-sm"><q-btn flat label="Cancelar" v-close-popup /><q-btn type="submit" color="primary" icon="save" label="Salvar modelo" :loading="busy" /></div>
            </q-form>
          </q-card-section></q-card>
      </q-dialog>

      <q-dialog v-model="sendDialog" aria-labelledby="send-dialog-title"><q-card class="notification-dialog-card"><q-card-section class="row items-center"><h2 id="send-dialog-title" class="text-h6 q-my-none">Solicitar envio</h2><q-space /><q-btn flat round dense icon="close" aria-label="Fechar solicitação" v-close-popup /></q-card-section><q-card-section>
          <p class="text-grey-7">A solicitação passa pelas regras de consentimento e é incluída na fila. O aceite do provedor não confirma a entrega.</p>
          <q-form class="q-gutter-md" @submit.prevent="requestNotification">
            <q-select v-model="selectedTemplateId" outlined label="Modelo ativo *" emit-value map-options
              :options="templates.filter((item) => item.isActive).map((item) => ({ label: `${item.purpose} · ${translateChannel(item.channel)} · ${item.language}`, value: item.id }))" />
            <q-input v-model="send.destination" outlined label="Destino *" maxlength="254" required hint="E-mail ou telefone que receberá a mensagem." />
            <q-input v-model="send.parametersJson" outlined type="textarea" label="Dados para preencher o modelo (JSON)" hint='Ex.: {"name":"Ana"} ou {"1":"Ana"} para WhatsApp.' />
            <q-input v-model="send.idempotencyKey" outlined label="Chave para evitar duplicidade *" maxlength="200" required hint="Repetir a mesma chave evita criar a mesma solicitação mais de uma vez." />
            <q-btn type="submit" color="primary" icon="send" label="Solicitar envio" :disable="!selectedTemplate" :loading="busy" />
          </q-form>
      </q-card-section></q-card></q-dialog>
    </div>

    <q-dialog v-model="explanationDialog" aria-labelledby="explanation-title"><q-card class="notification-dialog-card"><q-card-section class="row items-center"><h2 id="explanation-title" class="text-h6 q-my-none">Como as notificações funcionam</h2><q-space /><q-btn flat round dense icon="close" aria-label="Fechar explicação" v-close-popup /></q-card-section><q-card-section><ol><li>O modelo define finalidade, canal e conteúdo.</li><li>O consentimento registra a autorização para um destino. A revogação cria um novo evento e bloqueia envios que exigem autorização.</li><li>A solicitação valida as regras e entra na fila. “Aceito pelo provedor” significa que o serviço recebeu a solicitação, não que a mensagem foi entregue.</li></ol></q-card-section><q-card-actions align="right"><q-btn flat label="Fechar" v-close-popup /></q-card-actions></q-card></q-dialog>
    <q-dialog v-model="helpDialog" aria-labelledby="template-help-title"><q-card class="notification-dialog-card"><q-card-section class="row items-center"><h2 id="template-help-title" class="text-h6 q-my-none">Ajuda: finalidades e campos disponíveis</h2><q-space /><q-btn flat round dense icon="close" aria-label="Fechar ajuda" v-close-popup /></q-card-section><q-card-section><p>Para automatizar mensagens de pedidos públicos, use uma destas finalidades:</p><ul class="notification-code-list"><li>order.confirmed</li><li>order.preparing</li><li>order.ready</li><li>order.out_for_delivery</li><li>order.completed</li><li>order.cancelled</li><li>order.rejected</li></ul><p>Campos disponíveis: <code v-pre>{{name}}</code>, <code v-pre>{{orderNumber}}</code>, <code v-pre>{{status}}</code>, <code v-pre>{{statusLabel}}</code>, <code v-pre>{{trackingReference}}</code>, <code v-pre>{{trackingUrl}}</code>, <code v-pre>{{total}}</code> e <code v-pre>{{serviceType}}</code>.</p></q-card-section><q-card-actions align="right"><q-btn flat label="Fechar" v-close-popup /></q-card-actions></q-card></q-dialog>
    <q-dialog v-model="consentHistoryDialog" aria-labelledby="consent-history-title"><q-card class="notification-dialog-card"><q-card-section class="row items-center"><h2 id="consent-history-title" class="text-h6 q-my-none">Histórico de autorizações</h2><q-space /><q-btn flat round dense icon="close" aria-label="Fechar histórico" v-close-popup /></q-card-section><q-card-section><q-list bordered separator><q-item v-for="item in consentHistory" :key="item.id"><q-item-section><q-item-label>{{ item.isGranted ? 'Autorização concedida' : 'Autorização revogada' }}</q-item-label><q-item-label caption>{{ new Date(item.capturedAtUtc).toLocaleString('pt-BR') }} · {{ item.source || 'Origem não informada' }}</q-item-label></q-item-section></q-item></q-list></q-card-section><q-card-actions align="right"><q-btn flat label="Fechar" v-close-popup /></q-card-actions></q-card></q-dialog>
    <q-dialog v-model="revokeDialog" aria-labelledby="revoke-consent-title"><q-card class="notification-dialog-card"><q-card-section class="row items-center"><h2 id="revoke-consent-title" class="text-h6 q-my-none">Registrar revogação</h2><q-space /><q-btn flat round dense icon="close" aria-label="Cancelar revogação" v-close-popup /></q-card-section><q-card-section><p>Esta ação registra que a autorização foi revogada. O histórico anterior será preservado.</p><p v-if="selectedConsent"><strong>{{ selectedConsent.destination }}</strong> · {{ translateChannel(selectedConsent.channel) }} · {{ selectedConsent.purpose }}</p><q-form class="q-gutter-md" @submit.prevent="confirmRevocation"><q-input v-model="revocationSource" outlined label="Origem da revogação *" maxlength="200" required hint="Ex.: solicitação por e-mail, telefone ou atendimento." /><div class="row justify-end q-gutter-sm"><q-btn flat label="Cancelar" v-close-popup /><q-btn type="submit" color="negative" icon="block" label="Registrar revogação" :loading="busy" /></div></q-form></q-card-section></q-card></q-dialog>

    <q-dialog v-model="attemptDialog" aria-labelledby="attempt-dialog-title">
      <q-card style="min-width: min(36rem, 92vw)">
        <q-card-section class="row items-center">
          <h2 id="attempt-dialog-title" class="text-h6 q-my-none">Tentativas de envio</h2>
          <q-space />
          <q-btn flat round dense icon="close" aria-label="Fechar histórico" v-close-popup />
        </q-card-section>
        <q-card-section>
          <q-linear-progress v-if="attemptLoading" indeterminate />
          <q-list v-else bordered separator>
            <q-item v-for="attempt in attemptHistory" :key="attempt.attemptNumber">
              <q-item-section>
                <q-item-label>Tentativa {{ attempt.attemptNumber }} · {{ translateStatus(attempt.status) }}</q-item-label>
                <q-item-label caption>{{ new Date(attempt.createdAtUtc).toLocaleString('pt-BR') }} · {{ attempt.safeErrorCode || attempt.providerMessageId || 'Sem código de erro' }}</q-item-label>
              </q-item-section>
            </q-item>
            <q-item v-if="!attemptHistory.length"><q-item-section>Nenhuma tentativa registrada.</q-item-section></q-item>
          </q-list>
        </q-card-section>
        <q-card-actions align="right"><q-btn flat label="Fechar" v-close-popup /></q-card-actions>
      </q-card>
    </q-dialog>
  </q-page>
</template>

<style scoped>
.notification-section-heading p { margin: 4px 0 0; color: var(--zf-text-muted); }

.notification-workflow { display: grid; gap: 28px; }
.communications-page { box-sizing: border-box; width: 100%; max-width: 1600px; padding: 28px 24px 40px; }
.notification-section-heading { display: flex; align-items: flex-start; justify-content: space-between; gap: 24px; margin-bottom: 16px; }
.notification-tab-panel { padding: 24px; border-radius: var(--zf-border-radius); }
.notification-consent-layout { display: grid; grid-template-columns: minmax(0, 5fr) minmax(0, 7fr); gap: 40px; align-items: start; }
.consent-form-grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 16px; }
.consent-form-grid > .q-field { min-width: 0; width: 100%; }
.consent-form-wide { grid-column: 1 / -1; min-width: 0; }
.notification-empty-state { padding: 16px; border: 1px dashed var(--zf-border-subtle); border-radius: var(--zf-border-radius); }
.notification-empty-state p { max-width: 38ch; color: var(--zf-text-muted); }
.notification-code-list { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 4px 24px; padding-left: 20px; }
.header-description { display: flex; align-items: center; gap: 4px; }
.notification-dialog-card { width: min(640px, calc(100vw - 32px)); max-height: min(90vh, 900px); overflow-y: auto; }
.notification-dialog-card :deep(.q-card__section) { overflow-wrap: anywhere; }
.notification-template-content { padding: 16px 24px 24px; }
.notification-template-form { display: grid; gap: 16px; }
.notification-template-form > .q-field { min-width: 0; width: 100%; }
.notification-template-row { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 16px; align-items: start; }
.notification-template-row > .q-field { min-width: 0; width: 100%; }
.notification-template-form .notification-toggle-group { display: flex; flex-wrap: wrap; align-items: center; gap: 8px 24px; }
.notification-template-form > .row { margin: 0; }

@media (max-width: 768px) {
  .communications-page { padding: 20px 16px 32px; }
  .notification-workflow { gap: 16px; }
  .notification-section-heading { display: flex; flex-wrap: wrap; align-items: flex-start; gap: 12px; }
  .notification-section-heading > :first-child { flex: 1 1 100%; }
  .notification-section-heading :deep(.q-btn) { max-width: 100%; }
  .notification-section-heading :deep(.q-list) { overflow-wrap: anywhere; }
  .notification-section-heading :deep(.q-tabs__content) { overflow-x: auto; }
  .notification-tab-panel { padding: 20px; }
  .notification-consent-layout { grid-template-columns: 1fr; gap: 28px; }
  .notification-workflow :deep(.q-item__section--side) { align-items: flex-start; }
}

@media (max-width: 480px) {
  .communications-page { padding: 16px 12px 24px; }
  .notification-tab-panel { padding: 16px; }
  .consent-form-grid { grid-template-columns: 1fr; }
  .consent-form-wide { grid-column: auto; }
  .notification-code-list { grid-template-columns: 1fr; }
  .notification-template-content { padding: 12px 16px 20px; }
  .notification-template-row { grid-template-columns: 1fr; gap: 16px; }
}
</style>
