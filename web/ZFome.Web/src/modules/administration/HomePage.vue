<script setup lang="ts">
import { computed, onUnmounted, ref, watch } from 'vue'
import ProblemBanner from '../../components/ProblemBanner.vue'
import { useSessionStore } from '../session/store'
import { reportingClient, type BusinessDashboard, type DashboardDay, type ServiceType } from './reporting/client'

const session = useSessionStore()
const today = localDate(new Date())
const from = ref(`${today.slice(0, 7)}-01`)
const to = ref(today)
const serviceType = ref<ServiceType | null>(null)
const productPage = ref(1)
const refreshVersion = ref(0)
const report = ref<BusinessDashboard>()
const loading = ref(false)
const exporting = ref(false)
const error = ref<unknown>(null)
let controller: AbortController | undefined

const unit = computed(() =>
  session.units.find((item) => item.id === session.unitId)
)
const days = computed(() => {
  if (!report.value) return []
  const previous = new Map(
    report.value.series.filter((item) => item.isComparison).map((item) => [item.periodOffset, item])
  )
  return report.value.series
    .filter((item) => !item.isComparison)
    .map((item) => ({ current: item, previous: previous.get(item.periodOffset) }))
})
const chartMaximum = computed(() => Math.max(
  1,
  ...days.value.flatMap(({ current, previous }) => [current.revenue, previous?.revenue ?? 0])
))
const productPages = computed(() => Math.max(1, Math.ceil((report.value?.products.total ?? 0) / 10)))
const cancelledTotal = computed(() =>
  (report.value?.current.cancelledOrders ?? 0) + (report.value?.current.rejectedOrders ?? 0)
)
const shortcuts = computed(() => [
  { label: 'Catálogo', to: '/administration/catalog', available: session.can('management') },
  { label: 'Disponibilidade e horários', to: '/administration/availability', available: session.can('administration') },
  { label: 'Clientes', to: '/administration/customers', available: session.can('customer-operations') },
  { label: 'Regiões de entrega', to: '/administration/delivery-regions', available: session.can('management') },
  { label: 'Cupons', to: '/administration/coupons', available: session.can('promotion-management') },
  { label: 'Formas de pagamento', to: '/administration/payment-methods', available: session.can('payment-management') }
].filter((shortcut) => shortcut.available))

watch([() => session.unitId, from, to, serviceType, productPage, refreshVersion], async () => {
  if (!session.unitId) {
    report.value = undefined
    return
  }
  controller?.abort()
  const activeController = new AbortController()
  controller = activeController
  loading.value = true
  error.value = null
  report.value = undefined
  try {
    report.value = await reportingClient.dashboard(session.unitId, {
      from: from.value,
      to: to.value,
      serviceType: serviceType.value,
      productPage: productPage.value,
      productPageSize: 10
    }, activeController.signal)
  } catch (failure) {
    if (!activeController.signal.aborted) error.value = failure
  } finally {
    if (!activeController.signal.aborted) loading.value = false
  }
}, { immediate: true })

watch([from, to, serviceType], () => { productPage.value = 1 })
onUnmounted(() => controller?.abort())

async function exportReport() {
  if (!session.unitId || !report.value) return
  exporting.value = true
  error.value = null
  try {
    const content = await reportingClient.export(session.unitId, {
      from: from.value,
      to: to.value,
      serviceType: serviceType.value
    })
    const url = URL.createObjectURL(content)
    const anchor = document.createElement('a')
    anchor.href = url
    anchor.download = `relatorio-${from.value}-${to.value}.csv`
    anchor.click()
    URL.revokeObjectURL(url)
  } catch (failure) {
    error.value = failure
  } finally {
    exporting.value = false
  }
}

function revenueScale(day: DashboardDay | undefined): number {
  if (!day || day.revenue <= 0) return 0.015
  return Math.max(0.035, day.revenue / chartMaximum.value)
}

function dateLabel(value: string): string {
  return new Intl.DateTimeFormat('pt-BR', { day: '2-digit', month: 'short' })
    .format(new Date(`${value}T12:00:00`)).replace('.', '')
}

function money(value: number): string {
  return new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' }).format(value)
}

function quantity(value: number): string {
  return new Intl.NumberFormat('pt-BR', { maximumFractionDigits: 3 }).format(value)
}

function comparison(current: number, previous: number): string {
  if (previous === 0) return current === 0 ? 'Sem variação no período anterior' : 'Sem base no período anterior'
  const change = ((current - previous) / Math.abs(previous)) * 100
  const formatted = new Intl.NumberFormat('pt-BR', { maximumFractionDigits: 1 }).format(Math.abs(change))
  return `${change > 0 ? '+' : change < 0 ? '−' : ''}${formatted}% em relação ao período anterior`
}

function localDate(value: Date): string {
  return `${value.getFullYear()}-${String(value.getMonth() + 1).padStart(2, '0')}-${String(value.getDate()).padStart(2, '0')}`
}
</script>
<template>
  <q-page class="admin-page">
    <header class="admin-page-header">
      <div>
        <h1 class="text-h4 q-my-none">Visão do negócio</h1>
        <p>{{ unit ? `Acompanhe o desempenho de ${unit.name}.` : 'Nenhuma unidade autorizada disponível.' }}</p>
      </div>
      <q-btn color="primary" icon="download" label="Exportar relatório" :loading="exporting" :disable="!report" @click="exportReport" />
    </header>
    <ProblemBanner :error="error">
      <q-btn v-if="session.unitId" flat color="negative" icon="refresh" label="Tentar novamente" class="q-mt-sm" @click="refreshVersion++" />
    </ProblemBanner>
    <q-banner v-if="!unit" class="bg-amber-1 rounded-borders"
      >Solicite uma associação de unidade ao responsável pelo seu
      acesso.</q-banner
    >
    <template v-else>
      <section class="admin-home-context" aria-labelledby="active-unit-heading">
        <div>
          <p class="admin-home-context__label">UNIDADE ATIVA</p>
          <h2 id="active-unit-heading">{{ unit.name }}</h2>
          <p>A unidade selecionada define os dados exibidos nesta área. Cada operação continua protegida pelas permissões da sua conta.</p>
        </div>
        <q-chip outline color="primary">
          {{ session.context?.isPlatformUser ? 'Identidade de plataforma' : 'Acesso do estabelecimento' }}
        </q-chip>
      </section>

      <section class="report-filters" aria-label="Filtros do relatório">
        <q-input v-model="from" type="date" outlined dense label="De" />
        <q-input v-model="to" type="date" outlined dense label="Até" />
        <q-select v-model="serviceType" :options="[
          { label: 'Todas as modalidades', value: null },
          { label: 'Mesa', value: 'Table' },
          { label: 'Retirada', value: 'Pickup' },
          { label: 'Entrega', value: 'Delivery' }
        ]" emit-value map-options outlined dense label="Atendimento" />
        <span v-if="report" class="report-timezone">Fuso da unidade: {{ report.timeZoneId }}</span>
      </section>

      <div v-if="loading" class="report-loading" role="status" aria-live="polite">
        <q-spinner color="primary" size="28px" />
        <span>Atualizando indicadores…</span>
      </div>

      <template v-else-if="report">
        <section class="report-metrics" aria-label="Indicadores do período">
          <article class="report-metric report-metric--primary">
            <span>Faturamento concluído</span>
            <strong>{{ money(report.current.revenue) }}</strong>
            <small>{{ comparison(report.current.revenue, report.previous.revenue) }}</small>
          </article>
          <article class="report-metric">
            <span>Pedidos concluídos</span>
            <strong>{{ report.current.completedOrders }}</strong>
            <small>{{ report.current.receivedOrders }} recebidos no período</small>
          </article>
          <article class="report-metric">
            <span>Ticket médio</span>
            <strong>{{ money(report.current.averageTicket) }}</strong>
            <small>Calculado sobre pedidos concluídos</small>
          </article>
          <article class="report-metric">
            <span>Pagamentos confirmados</span>
            <strong>{{ money(report.current.confirmedPayments) }}</strong>
            <small>Indicador financeiro independente</small>
          </article>
          <article class="report-metric report-metric--status">
            <span>Cancelados e rejeitados</span>
            <strong>{{ cancelledTotal }}</strong>
            <small>{{ report.current.cancelledOrders }} cancelados · {{ report.current.rejectedOrders }} rejeitados</small>
          </article>
        </section>

        <div class="report-content-grid">
          <q-card flat bordered class="report-panel report-trend">
            <q-card-section class="report-panel-heading">
              <div>
                <h2>Faturamento por dia</h2>
                <p>Pedidos concluídos no fuso local da unidade</p>
              </div>
              <div class="report-legend" aria-label="Legenda da comparação">
                <span><i class="report-legend__current" />Este período</span>
                <span><i class="report-legend__previous" />Anterior</span>
              </div>
            </q-card-section>
            <q-card-section v-if="days.length" class="report-chart-scroll">
              <div class="report-chart" role="list" aria-label="Comparação diária do faturamento">
                <div v-for="({ current, previous }, index) in days" :key="current.date" class="report-chart-day" role="listitem"
                  :aria-label="`${dateLabel(current.date)}: período atual ${money(current.revenue)}, período anterior ${money(previous?.revenue ?? 0)}`">
                  <div class="report-chart-day__bars">
                    <span class="report-chart-bar report-chart-bar--previous" :style="{ transform: `scaleY(${revenueScale(previous)})` }" aria-hidden="true" />
                    <span class="report-chart-bar report-chart-bar--current" :style="{ transform: `scaleY(${revenueScale(current)})` }" aria-hidden="true" />
                  </div>
                  <small v-if="days.length <= 16 || index % Math.ceil(days.length / 16) === 0">{{ dateLabel(current.date) }}</small>
                </div>
              </div>
            </q-card-section>
            <q-card-section v-else class="report-empty">Sem dias para exibir neste período.</q-card-section>
            <q-card-section class="report-chart-summary">
              <span>{{ money(report.current.revenue) }} no período</span>
              <span>{{ money(report.previous.revenue) }} no período anterior</span>
            </q-card-section>
          </q-card>

          <q-card flat bordered class="report-panel report-products">
            <q-card-section class="report-panel-heading">
              <div>
                <h2>Produtos mais vendidos</h2>
                <p>Composição dos pedidos concluídos</p>
              </div>
            </q-card-section>
            <q-card-section v-if="report.products.total" class="report-product-list">
              <div v-for="(product, index) in report.products.items" :key="product.name" class="report-product-row">
                <span class="report-product-rank">{{ (productPage - 1) * 10 + index + 1 }}</span>
                <div class="report-product-info">
                  <div><strong>{{ product.name }}</strong><span>{{ quantity(product.quantitySold) }} un. · {{ money(product.revenue) }}</span></div>
                  <q-linear-progress rounded size="5px" :value="product.quantitySold / Math.max(1, ...report.products.items.map((item) => item.quantitySold))" color="primary" />
                </div>
              </div>
              <q-pagination v-if="productPages > 1" v-model="productPage" :max="productPages" :max-pages="5" direction-links boundary-links color="primary" aria-label="Páginas de produtos mais vendidos" />
            </q-card-section>
            <q-card-section v-else class="report-empty">Os produtos aparecerão após os primeiros pedidos concluídos.</q-card-section>
          </q-card>
        </div>

        <p class="report-definition">Vendas e produtos consideram pedidos concluídos. Pagamentos confirmados são exibidos à parte; cancelamentos e rejeições não entram no faturamento.</p>
      </template>

      <section v-if="shortcuts.length" class="admin-home-shortcuts" aria-labelledby="shortcuts-heading">
        <h2 id="shortcuts-heading" class="text-h6">Acessos frequentes</h2>
        <div class="admin-home-shortcuts__list">
          <q-btn
            v-for="shortcut in shortcuts"
            :key="shortcut.to"
            class="admin-home-shortcut"
            :to="shortcut.to"
            :label="shortcut.label"
            no-caps
            outline
            color="primary"
          />
        </div>
      </section>
    </template>
  </q-page>
</template>

<style scoped>
.admin-home-context {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 24px;
  padding: 24px;
  border: 0;
  border-radius: var(--zf-border-radius);
  background: linear-gradient(115deg, #5144bd 0%, #6256c8 62%, #6d61d1 100%);
  color: #fff;
  box-shadow: 0 12px 28px rgb(81 68 189 / 18%);
}
.admin-home-context h2 { margin: 0; font-size: 1.5rem; }
.admin-home-context p { max-width: 68ch; margin: 8px 0 0; color: rgb(255 255 255 / 86%); }
.admin-home-context .admin-home-context__label { margin: 0 0 6px; color: #e0dcff; font-size: .75rem; font-weight: 800; letter-spacing: .08em; }
.admin-home-context :deep(.q-chip) { color: #fff !important; border-color: rgb(255 255 255 / 55%) !important; }
.admin-home-shortcuts { margin-top: 32px; }
.admin-home-shortcuts h2 { margin-bottom: 14px; }
.admin-home-shortcuts__list { display: grid; grid-template-columns: repeat(auto-fit, minmax(min(100%, 230px), 1fr)); gap: 12px; }
.admin-home-shortcut { min-height: 56px; justify-content: flex-start; }
.admin-home-shortcut :deep(.q-btn__content) { justify-content: flex-start; width: 100%; }
.report-filters {
  display: grid;
  grid-template-columns: repeat(2, minmax(150px, 190px)) minmax(210px, 260px) 1fr;
  align-items: center;
  gap: 12px;
  margin: 28px 0 18px;
}
.report-timezone { justify-self: end; color: var(--zf-text-muted); font-size: .875rem; }
.report-loading { display: flex; min-height: 240px; align-items: center; justify-content: center; gap: 12px; color: var(--zf-text-muted); }
.report-metrics {
  display: grid;
  grid-template-columns: repeat(5, minmax(0, 1fr));
  gap: 14px;
}
.report-metric {
  position: relative;
  display: grid;
  align-content: start;
  gap: 8px;
  min-width: 0;
  padding: 20px;
  overflow: hidden;
  border: 1px solid var(--zf-border-subtle);
  border-radius: var(--zf-border-radius);
  background: var(--zf-surface-raised);
  box-shadow: 0 4px 16px rgb(31 41 55 / 4%);
}
.report-metric--primary { border-top: 3px solid var(--zf-admin-accent); }
.report-metric > span { color: var(--zf-text-muted); font-size: .82rem; font-weight: 650; }
.report-metric strong { overflow-wrap: anywhere; font-size: clamp(1.25rem, 2vw, 1.8rem); font-variant-numeric: tabular-nums; line-height: 1.15; }
.report-metric small { color: var(--zf-text-muted); line-height: 1.35; }
.report-metric--primary strong { color: var(--zf-admin-accent-text); }
.report-metric--status strong { color: var(--zf-status-warning-text); }
.report-content-grid { display: grid; grid-template-columns: minmax(0, 1.65fr) minmax(300px, 1fr); gap: 18px; margin-top: 20px; }
.report-panel { min-width: 0; border-radius: var(--zf-border-radius); background: var(--zf-surface-raised); box-shadow: 0 4px 16px rgb(31 41 55 / 4%); }
.report-panel-heading { display: flex; align-items: flex-start; justify-content: space-between; gap: 18px; }
.report-panel-heading h2 { margin: 0; font-size: 1.1rem; font-weight: 700; }
.report-panel-heading p { margin: 5px 0 0; color: var(--zf-text-muted); font-size: .875rem; }
.report-legend { display: flex; flex-wrap: wrap; gap: 12px; color: var(--zf-text-muted); font-size: .78rem; }
.report-legend span { display: inline-flex; align-items: center; gap: 6px; white-space: nowrap; }
.report-legend i { width: 9px; height: 9px; border-radius: 2px; }
.report-legend__current { background: var(--zf-admin-accent); }
.report-legend__previous { background: var(--zf-border-subtle); }
.report-chart-scroll { overflow-x: auto; padding-top: 4px; }
.report-chart { display: grid; grid-auto-columns: minmax(28px, 1fr); grid-auto-flow: column; gap: 8px; min-width: 480px; height: 190px; padding-top: 12px; border-bottom: 1px solid var(--zf-border-subtle); }
.report-chart-day { display: grid; grid-template-rows: 1fr 20px; justify-items: center; min-width: 0; }
.report-chart-day__bars { display: flex; width: 100%; align-items: flex-end; justify-content: center; gap: 3px; }
.report-chart-bar { width: min(12px, 38%); height: 100%; border-radius: 4px 4px 0 0; transform-origin: bottom; transition: transform .24s ease-out; }
.report-chart-bar--current { background: var(--zf-admin-accent); }
.report-chart-bar--previous { background: var(--zf-border-subtle); }
.report-chart-day small { color: var(--zf-text-muted); font-size: .68rem; white-space: nowrap; }
.report-chart-summary { display: flex; flex-wrap: wrap; justify-content: space-between; gap: 8px 16px; color: var(--zf-text-muted); font-size: .82rem; }
.report-chart-summary span:first-child { color: var(--zf-admin-accent-text); font-weight: 700; }
.report-product-list { display: grid; gap: 16px; }
.report-product-row { display: grid; grid-template-columns: 28px minmax(0, 1fr); align-items: start; gap: 10px; }
.report-product-rank { display: grid; width: 26px; height: 26px; place-items: center; border-radius: 50%; background: var(--zf-admin-accent-soft); color: var(--zf-admin-accent-text); font-size: .75rem; font-weight: 750; }
.report-product-info { display: grid; min-width: 0; gap: 8px; }
.report-product-info > div { display: flex; justify-content: space-between; gap: 12px; }
.report-product-info strong { min-width: 0; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.report-product-info span { flex: 0 0 auto; color: var(--zf-text-muted); font-size: .8rem; font-variant-numeric: tabular-nums; }
.report-empty { display: grid; min-height: 120px; place-items: center; color: var(--zf-text-muted); text-align: center; }
.report-definition { margin: 14px 0 0; color: var(--zf-text-muted); font-size: .8rem; }
@media (prefers-reduced-motion: reduce) {
  .report-chart-bar { transition: none; }
}
@media (max-width: 600px) {
  .admin-home-context { align-items: flex-start; flex-direction: column; padding: 18px; }
}
@media (max-width: 900px) {
  .report-content-grid { grid-template-columns: minmax(0, 1fr); }
  .report-metrics { grid-template-columns: repeat(3, minmax(0, 1fr)); }
  .report-metric:nth-child(4) { border-left: 0; }
}
@media (max-width: 640px) {
  .report-filters { grid-template-columns: repeat(2, minmax(0, 1fr)); }
  .report-filters :deep(.q-select) { grid-column: 1 / -1; }
  .report-timezone { grid-column: 1 / -1; justify-self: start; }
  .report-metrics { grid-template-columns: repeat(2, minmax(0, 1fr)); }
  .report-metric { padding: 16px 14px; }
  .report-metrics { gap: 10px; }
  .report-panel-heading { flex-direction: column; }
  .report-product-info > div { flex-direction: column; gap: 3px; }
}
</style>
