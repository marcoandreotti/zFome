<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import QRCode from 'qrcode'
import { useSessionStore } from '../../session/store'
import ProblemBanner from '../../../components/ProblemBanner.vue'
import AccessStep from './AccessStep.vue'
import { onboardingClient as client, tableIntent, publicTableUrl, type Configuration, type Progress, type Table } from './client'
const session = useSessionStore(), route = useRoute(), router = useRouter()
const steps = [{ id: 'dados', label: 'Dados' }, { id: 'tema', label: 'Tema' }, { id: 'horarios', label: 'Horários' }, { id: 'mesas', label: 'Mesas e QR' }, { id: 'acessos', label: 'Acessos' }, { id: 'revisao', label: 'Revisão' }]
const step = computed(() => String(route.params.step || 'dados'))
const current = computed(() => steps.findIndex(s => s.id === step.value))
const configuration = ref<Configuration>(), progress = ref<Progress>()
const tables = ref<Table[]>([]), page = ref(1), total = ref(0)
const loading = ref(false), busy = ref(false), error = ref<unknown>(null), message = ref('')
const draft = ref(tableIntent(session.unitId)), editing = ref<Table | null>(null)
const qr = ref<{ image: string; url: string; code: string } | null>(null)
const rotating = ref<Table | null>(null)
const days = ['Domingo', 'Segunda', 'Terça', 'Quarta', 'Quinta', 'Sexta', 'Sábado'].map((label, value) => ({ label, value }))
const colors = [{ key: 'primaryColor', label: 'Cor primária' }, { key: 'secondaryColor', label: 'Cor secundária' }, { key: 'backgroundColor', label: 'Cor de fundo' }, { key: 'textColor', label: 'Cor do texto' }] as const
const required = (v: string) => !!v?.trim() || 'Campo obrigatório'
let alive = true
const request = new AbortController()
async function refresh() { const result = await client.progress(session.unitId, request.signal); if (alive) progress.value = result }
async function loadTables() {
  const result = await client.tables(session.unitId, page.value, request.signal)
  if (alive) { tables.value = result.items; total.value = result.totalCount }
}
async function load() {
  loading.value = true; error.value = null
  try {
    const [config] = await Promise.all([client.configuration(session.unitId, request.signal), refresh(), loadTables()])
    if (alive) configuration.value = config
  } catch (e) { if (alive) error.value = e }
  finally { if (alive) loading.value = false }
}
async function run(operation: () => Promise<void>) {
  if (busy.value) return
  busy.value = true; error.value = null; message.value = ''
  try { await operation(); if (alive) { await refresh(); message.value = 'Configuração salva.' } }
  catch (e) { if (alive) error.value = e }
  finally { if (alive) busy.value = false }
}
async function go(id: string) {
  await run(async () => { await refresh(); if (alive) await router.push(`/administration/onboarding/${id}`) })
}
async function save() {
  const c = configuration.value
  if (!c) return
  await run(async () => {
    if (step.value === 'dados') await client.data(session.unitId, c.tradeName, c.slug)
    if (step.value === 'tema') await client.theme(session.unitId, c.theme)
    if (step.value === 'horarios') await client.hours(session.unitId, c.hours)
    await refresh()
    if (alive && (step.value !== 'horarios' || progress.value?.hoursReady)) await router.push(`/administration/onboarding/${steps[current.value + 1]?.id ?? 'revisao'}`)
  })
}
async function createTable() {
  await run(async () => {
    sessionStorage.setItem(`onboarding-table:${session.unitId}`, JSON.stringify(draft.value))
    await client.createTable(session.unitId, draft.value)
    sessionStorage.removeItem(`onboarding-table:${session.unitId}`)
    if (!alive) return
    draft.value = tableIntent(session.unitId); await loadTables()
  })
}
async function saveTable() {
  const table = editing.value
  if (!table) return
  await run(async () => { await client.updateTable(session.unitId, table); if (alive) { editing.value = null; qr.value = null; await loadTables() } })
}
async function rotate() {
  const table = rotating.value
  if (!table) return
  await run(async () => { await client.rotate(session.unitId, table.id); if (alive) { rotating.value = null; qr.value = null; await loadTables() } })
}
async function showQr(table: Table) {
  if (!table.publicPath) return
  await run(async () => {
    const url = publicTableUrl(table.publicPath!)
    const image = await QRCode.toDataURL(url, { width: 320, margin: 4 })
    if (alive) qr.value = { image, url, code: table.code }
  })
}
async function complete() {
  await run(async () => { await client.complete(session.unitId); await refresh() })
  if (!error.value && alive) message.value = 'Onboarding concluído. A unidade está pronta para operar.'
}
onMounted(load)
onUnmounted(() => { alive = false; request.abort() })
</script>
<template>
  <q-page class="admin-page onboarding-page">
    <header class="admin-page-header"><div><p class="admin-page-eyebrow">CONFIGURAÇÃO DA UNIDADE</p><h1 class="text-h4 q-my-sm">Prepare seu estabelecimento</h1><p>Salve cada etapa e continue quando quiser. Tema e mesas são opcionais.</p></div></header>
    <nav aria-label="Etapas de configuração" class="row q-gutter-sm q-mb-lg">
      <q-btn v-for="s in steps" :key="s.id" :label="s.label" :outline="step !== s.id" :aria-current="step === s.id ? 'step' : undefined" color="primary" no-caps :disable="busy || loading" @click="go(s.id)" />
    </nav>
    <ProblemBanner :error="error" />
    <p role="status" aria-live="polite">{{ loading ? 'Carregando configuração…' : message }}</p>
    <q-btn v-if="!configuration && !loading" label="Tentar novamente" @click="load" />
    <template v-if="configuration && progress">
      <p v-if="progress.completedAt" class="text-caption">Primeira conclusão: {{ new Date(progress.completedAt).toLocaleString('pt-BR') }}. A prontidão reflete a configuração atual.</p>
      <q-card flat bordered><q-card-section>
        <h2 class="text-h6 q-mt-none">{{ steps[current]?.label }}</h2>
        <q-form v-if="['dados', 'tema', 'horarios'].includes(step)" @submit="save">
          <template v-if="step === 'dados'">
            <q-input v-model="configuration.tradeName" label="Nome do estabelecimento" outlined maxlength="150" :rules="[required]" :disable="busy" />
            <q-input v-model="configuration.slug" label="Endereço público (slug)" outlined maxlength="100" :rules="[required, v => /^[a-z0-9]+(?:-[a-z0-9]+)*$/.test(v) || 'Use letras minúsculas, números e hífens']" :disable="busy" />
            <p class="text-caption">Alterar o endereço público exige compartilhar novos links e reimprimir os QR Codes.</p>
          </template>
          <div v-if="step === 'tema'" class="row q-col-gutter-md">
            <q-input v-for="color in colors" :key="color.key" v-model="configuration.theme[color.key]" :label="color.label" outlined clearable class="col-12 col-sm-6" :rules="[v => !v || /^#[a-fA-F0-9]{6}$/.test(v) || 'Use #RRGGBB']" :disable="busy" />
            <q-input v-model="configuration.theme.fontFamily" label="Fonte" outlined maxlength="100" clearable class="col-12" :disable="busy" />
            <q-input v-model="configuration.theme.logoUrl" label="URL do logotipo" type="url" outlined clearable maxlength="500" class="col-12" :disable="busy" />
            <q-input v-model="configuration.theme.faviconUrl" label="URL do favicon" type="url" outlined clearable maxlength="500" class="col-12" :disable="busy" />
            <p class="col-12">Campos vazios usam o tema padrão.</p>
          </div>
          <template v-if="step === 'horarios'">
            <p>Dias sem intervalos ficam fechados. A abertura e o fechamento devem ocorrer no mesmo dia.</p>
            <div v-for="(hours, index) in configuration.hours" :key="index" class="row q-col-gutter-sm q-mb-md">
              <q-select v-model="hours.dayOfWeek" :options="days" emit-value map-options label="Dia da semana" outlined class="col-12 col-sm-4" :disable="busy" />
              <q-input v-model="hours.opensAt" type="time" label="Abertura" outlined class="col-6 col-sm-3" :rules="[required]" :disable="busy" />
              <q-input v-model="hours.closesAt" type="time" label="Fechamento" outlined class="col-6 col-sm-3" :rules="[required, v => v > hours.opensAt || 'Feche após a abertura']" :disable="busy" />
              <div class="col-12 col-sm-2 hours-remove-cell"><q-btn flat square color="negative" icon="delete" class="collection-action-btn hours-remove-btn" :aria-label="'Remover intervalo ' + (index + 1)" :disable="busy" @click="configuration.hours.splice(index, 1)"><q-tooltip>Remover intervalo</q-tooltip></q-btn></div>
            </div>
            <q-btn label="Adicionar intervalo" icon="add" outline class="q-mb-md" :disable="busy || configuration.hours.length >= 100" @click="configuration.hours.push({ dayOfWeek: 1, opensAt: '09:00', closesAt: '18:00' })" />
          </template>
          <div class="q-mt-md"><q-btn type="submit" icon="save" label="Salvar e continuar" color="primary" :loading="busy" :disable="busy" /></div>
        </q-form>
        <template v-if="step === 'mesas'">
          <p>Mesas são opcionais. Uma nova tentativa após falha de comunicação reutiliza a intenção de cadastro.</p>
          <q-form @submit="createTable" class="row q-col-gutter-sm q-mb-lg">
            <q-input v-model="draft.code" label="Código da mesa" outlined maxlength="30" :rules="[required]" class="col-12 col-sm-4" :disable="busy" />
            <q-input v-model="draft.description" label="Descrição" outlined maxlength="100" class="col-12 col-sm-5" :disable="busy" />
            <div class="col-12 col-sm-3"><q-btn type="submit" icon="add" label="Criar mesa" color="primary" :disable="busy" /></div>
          </q-form>
          <q-card v-for="table in tables" :key="table.id" flat bordered class="q-mb-sm"><q-card-section class="row items-center q-gutter-sm">
            <div class="col"><strong>{{ table.code }}</strong> · {{ table.description }} · {{ table.isActive ? 'Ativa' : 'Inativa' }}</div>
            <q-btn flat square class="collection-action-btn" color="primary" icon="edit" :aria-label="'Editar mesa ' + table.code" :disable="busy" @click="editing = { ...table }"><q-tooltip>Editar mesa {{ table.code }}</q-tooltip></q-btn>
            <q-btn flat square class="collection-action-btn" color="secondary" icon="qr_code_2" :aria-label="'QR Code da mesa ' + table.code" :disable="busy || !table.isActive" @click="showQr(table)"><q-tooltip>QR Code da mesa {{ table.code }}</q-tooltip></q-btn>
            <q-btn flat square class="collection-action-btn" color="warning" text-color="dark" icon="key" :aria-label="'Renovar token da mesa ' + table.code" :disable="busy" @click="rotating = table"><q-tooltip>Renovar token da mesa {{ table.code }}</q-tooltip></q-btn>
          </q-card-section></q-card>
          <p v-if="!tables.length">Nenhuma mesa cadastrada.</p>
          <q-pagination v-if="total > 20" v-model="page" :max="Math.ceil(total / 20)" :disable="busy" @update:model-value="run(loadTables)" />
        </template>
        <AccessStep v-if="step === 'acessos'" @changed="run(refresh)" />
        <template v-if="step === 'revisao'">
          <dl><dt>Dados</dt><dd>{{ progress.dataReady ? 'Prontos' : 'Pendentes' }}</dd><dt>Horários</dt><dd>{{ progress.hoursReady ? 'Configurados' : 'Adicione um horário válido' }}</dd><dt>Acessos</dt><dd>{{ progress.accessReady ? 'Administrador ativo associado' : 'Associe um administrador ativo' }}</dd><dt>Mesas ativas (opcional)</dt><dd>{{ progress.activeTables }}</dd></dl>
          <p v-if="!progress.isReady" role="status">Etapas pendentes: {{ progress.pendingSteps.join(', ') }}.</p>
          <q-btn label="Atualizar prontidão" outline :disable="busy" @click="run(refresh)" class="q-mr-sm" />
          <q-btn label="Concluir configuração" icon="check" color="primary" :disable="busy || !progress.isReady" @click="complete" />
        </template>
      </q-card-section></q-card>
      <footer class="row justify-between q-mt-lg">
        <q-btn v-if="current > 0" label="Voltar" outline :disable="busy" @click="go(steps[current - 1]!.id)" />
        <q-btn v-if="['mesas', 'acessos'].includes(step)" label="Continuar" color="primary" :disable="busy" @click="go(steps[current + 1]!.id)" />
      </footer>
    </template>
    <q-dialog :model-value="!!editing" @update:model-value="!busy && (editing = null)" persistent>
      <q-card v-if="editing" style="width: 480px; max-width: 95vw"><q-card-section><h2 class="text-h6">Editar mesa</h2><ProblemBanner :error="error" /><q-form @submit="saveTable">
        <q-input v-model="editing.code" label="Código" outlined maxlength="30" :rules="[required]" :disable="busy" /><q-input v-model="editing.description" label="Descrição" outlined maxlength="100" :disable="busy" /><q-checkbox v-model="editing.isActive" label="Mesa ativa" :disable="busy" />
        <div><q-btn label="Cancelar" flat :disable="busy" @click="editing = null" /><q-btn label="Salvar mesa" icon="save" type="submit" color="primary" :disable="busy" /></div>
      </q-form></q-card-section></q-card>
    </q-dialog>
    <q-dialog :model-value="!!rotating" persistent><q-card><q-card-section><h2 class="text-h6">Renovar token da mesa {{ rotating?.code }}?</h2><p>O QR Code anterior deixará de funcionar imediatamente. Reimprima o novo QR Code.</p><ProblemBanner :error="error" /></q-card-section><q-card-actions><q-btn label="Cancelar" :disable="busy" @click="rotating = null" /><q-btn label="Renovar token" icon="key" color="warning" text-color="dark" :disable="busy" @click="rotate" /></q-card-actions></q-card></q-dialog>
    <q-dialog :model-value="!!qr" @update:model-value="qr = null"><q-card v-if="qr"><q-card-section class="text-center"><h2 class="text-h6">Mesa {{ qr.code }}</h2><img :src="qr.image" :alt="`QR Code da mesa ${qr.code}`" width="320" height="320" style="max-width: 100%; height: auto" /><p><a :href="qr.url" target="_blank" rel="noopener">Abrir página pública da mesa</a></p><a :href="qr.image" :download="`mesa-${qr.code}.png`">Baixar QR Code</a><div class="q-mt-md"><q-btn label="Fechar" @click="qr = null" /></div></q-card-section></q-card></q-dialog>
  </q-page>
</template>
<style scoped>
.onboarding-page { max-width: 1050px; margin: 0 auto; }
.hours-remove-cell { display: flex; align-items: flex-start; }
.hours-remove-btn { align-self: flex-start; height: 56px; }
dt { font-weight: 600; margin-top: 1rem; }
dd { margin: .25rem 0 1rem; }
</style>
