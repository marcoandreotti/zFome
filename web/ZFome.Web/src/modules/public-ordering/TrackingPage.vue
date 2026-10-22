<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { RouterLink, useRoute } from 'vue-router'
import ProblemBanner from '../../components/ProblemBanner.vue'
import { publicOrderingClient } from './client'
import { receiptStorage } from './cart'
import type { Tracking } from './types'
import { pollingDelay, terminalOrderStatuses } from './tracking'

const route = useRoute()
const reference = computed(() => String(route.params.reference))
const homeSlug = computed(() => receiptStorage.findSlug(reference.value))
const homePath = computed(() => homeSlug.value ? `/order/${encodeURIComponent(homeSlug.value)}` : '/')
const homeLabel = computed(() => homeSlug.value ? 'Voltar ao cardápio' : 'Ir para a página inicial')
const order = ref<Tracking>()
const relatedOrders = ref<Tracking[]>([])
const error = ref<unknown>(null)
const loading = ref(true)
const cancelling = ref(false)
const reviewAvailable = ref(false)
const reviewPending = ref(false)
const reason = ref('')
let timer: ReturnType<typeof setTimeout> | undefined
let failures = 0
const statusLabel: Record<string, string> = {
  Confirmed: 'Pedido confirmado', Preparing: 'Em preparação', Ready: 'Pronto',
  OutForDelivery: 'Saiu para entrega', Completed: 'Concluído',
  Cancelled: 'Cancelado', Rejected: 'Não aceito'
}
const statusTone: Record<string, string> = {
  Confirmed: 'confirmed', Preparing: 'preparing', Ready: 'ready',
  OutForDelivery: 'info', Completed: 'success', Cancelled: 'danger', Rejected: 'danger'
}
const canCancel = computed(() => order.value?.status === 'Confirmed')
function formatScheduledLabel(scheduledAtUtc: string | null | undefined, timeZone?: string | null) {
  if (!scheduledAtUtc) return null
  try {
    const formatted = new Intl.DateTimeFormat('pt-BR', {
      timeZone: timeZone ?? undefined,
      day: '2-digit',
      month: '2-digit',
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit'
    }).format(new Date(scheduledAtUtc))
    return `Agendado para ${formatted}`
  } catch {
    return `Agendado para ${new Date(scheduledAtUtc).toLocaleString('pt-BR')}`
  }
}
const scheduledLabel = computed(() => formatScheduledLabel(order.value?.scheduledAtUtc, order.value?.scheduledTimeZoneId))
const money = (value: number) => new Intl.NumberFormat('pt-BR', {
  style: 'currency', currency: 'BRL'
}).format(value)
function schedule() {
  clearTimeout(timer)
  if (order.value && terminalOrderStatuses.has(order.value.status)) return
  timer = setTimeout(refresh, pollingDelay(failures))
}
async function refresh() {
  if (document.visibilityState === 'hidden') { schedule(); return }
  try {
    order.value = await publicOrderingClient.track(reference.value)
    const references = homeSlug.value ? receiptStorage.readAll(homeSlug.value) : []
    const related = await Promise.allSettled(references.map(item => publicOrderingClient.track(item)))
    relatedOrders.value = related
      .filter((result): result is PromiseFulfilledResult<Tracking> => result.status === 'fulfilled')
      .map(result => result.value)
    reviewAvailable.value = false
    reviewPending.value = false
    if (order.value.status === 'Completed') {
      try {
        const progress = await publicOrderingClient.reviewProgress(reference.value)
        reviewAvailable.value = progress.canReview
        reviewPending.value = progress.pendingApproval
      } catch { /* Falha de reviews não afeta o acompanhamento do pedido. */ }
    }
    error.value = null
    failures = 0
  } catch (failure) {
    error.value = failure
    failures++
  } finally { loading.value = false; schedule() }
}
async function cancel() {
  if (!canCancel.value || cancelling.value) return
  cancelling.value = true
  error.value = null
  try {
    await publicOrderingClient.cancel(reference.value, reason.value.trim() || null)
    await refresh()
  } catch (failure) { error.value = failure } finally { cancelling.value = false }
}
watch(reference, () => {
  order.value = undefined
  relatedOrders.value = []
  error.value = null
  failures = 0
  void refresh()
})
function visible() { if (document.visibilityState === 'visible') void refresh() }
onMounted(() => { document.addEventListener('visibilitychange', visible); void refresh() })
onBeforeUnmount(() => { clearTimeout(timer); document.removeEventListener('visibilitychange', visible) })
</script>

<template>
  <q-page id="main-content" class="ordering-page">
    <main class="flow-panel tracking-panel">
      <header class="tracking-heading">
        <div>
          <p class="tracking-eyebrow">PEDIDO EM TEMPO REAL</p>
          <h1>Acompanhe seu pedido</h1>
        </div>
        <q-btn flat icon="arrow_back" :label="homeLabel" class="tracking-home-button" :to="homePath" />
      </header>
      <nav v-if="relatedOrders.length > 1" class="tracking-orders" aria-label="Pedidos recentes">
        <p class="tracking-eyebrow">PEDIDOS RECENTES</p>
        <div class="tracking-orders__list">
          <RouterLink v-for="item in relatedOrders" :key="item.reference" :to="`/order/track/${encodeURIComponent(item.reference)}`"
            class="tracking-order-link" :class="{ 'tracking-order-link--active': item.reference === reference }">
            <strong>Pedido nº {{ item.number }}</strong>
            <span>{{ statusLabel[item.status] ?? item.status }}</span>
            <small v-if="item.scheduledAtUtc">{{ formatScheduledLabel(item.scheduledAtUtc, item.scheduledTimeZoneId) }}</small>
          </RouterLink>
        </div>
      </nav>
      <ProblemBanner :error="error">
        <q-btn flat label="Tentar novamente" @click="refresh" />
      </ProblemBanner>
      <div v-if="loading" class="tracking-loading" role="status">
        <q-spinner color="primary" size="32px" />
        <span>Buscando as atualizações do seu pedido…</span>
      </div>
      <template v-else-if="order">
        <section class="tracking-status-card" aria-live="polite">
          <div class="tracking-status-card__icon" :class="`tracking-status-card__icon--${statusTone[order.status] ?? 'info'}`">
            <q-icon :name="order.status === 'Completed' ? 'check_circle' : order.status === 'Cancelled' || order.status === 'Rejected' ? 'error' : 'schedule'" size="30px" aria-hidden="true" />
          </div>
          <div class="tracking-status-card__copy">
            <span>Pedido nº {{ order.number }}</span>
            <h2 :class="`tracking-status--${statusTone[order.status] ?? 'info'}`">{{ statusLabel[order.status] ?? order.status }}</h2>
            <p v-if="order.serviceType === 'Delivery' && order.deliveryEstimatedMinutes">
              <q-icon name="local_shipping" aria-hidden="true" />
              Previsão de entrega: <strong>{{ order.deliveryEstimatedMinutes }} min</strong>
              <span v-if="order.deliveryRegionName"> · {{ order.deliveryRegionName }}</span>
            </p>
            <p v-else-if="order.serviceType === 'Pickup'"><q-icon name="storefront" aria-hidden="true" /> Retirada no estabelecimento</p>
            <p v-else><q-icon name="table_restaurant" aria-hidden="true" /> Consumo no local</p>
            <p v-if="scheduledLabel" class="tracking-scheduled"><q-icon name="event" aria-hidden="true" /> <strong>{{ scheduledLabel }}</strong></p>
          </div>
          <span v-if="!terminalOrderStatuses.has(order.status)" class="tracking-live"><span aria-hidden="true"></span> Atualizando</span>
        </section>

        <q-btn v-if="reviewAvailable || reviewPending" color="primary" icon="star" :label="reviewPending ? 'Ver avaliação enviada' : 'Avaliar pedido'" :to="`/order/review/${encodeURIComponent(reference)}`" class="review-invitation" />

        <div class="tracking-grid">
          <section class="tracking-card tracking-history">
            <div class="tracking-card__heading">
              <div><p class="tracking-eyebrow">ACOMPANHAMENTO</p><h3>Etapas do pedido</h3></div>
              <q-icon name="timeline" size="24px" aria-hidden="true" />
            </div>
            <ol class="timeline" aria-label="Histórico do pedido">
              <li v-for="(event, index) in order.history" :key="event.occurredAt + event.status" :class="{ 'timeline__item--latest': index === order.history.length - 1 }">
                <span class="timeline__marker"><q-icon :name="index === order.history.length - 1 ? 'radio_button_checked' : 'check'" size="16px" aria-hidden="true" /></span>
                <div class="timeline__content">
                  <strong :class="`tracking-status--${statusTone[event.status] ?? 'info'}`">{{ statusLabel[event.status] ?? event.status }}</strong>
                  <time :datetime="event.occurredAt">{{ new Date(event.occurredAt).toLocaleString('pt-BR') }}</time>
                  <span v-if="event.note">{{ event.note }}</span>
                </div>
              </li>
            </ol>
          </section>

          <aside class="tracking-side-column">
            <section class="tracking-card tracking-summary">
              <div class="tracking-card__heading">
                <div><p class="tracking-eyebrow">RESUMO</p><h3>Seu pedido</h3></div>
                <q-icon name="receipt" size="24px" aria-hidden="true" />
              </div>
              <ul class="tracking-items">
                <li v-for="(item, index) in order.items" :key="`${item.productName}-${index}`">
                  <span class="tracking-item-quantity">{{ item.quantity }}×</span>
                  <span class="tracking-item-copy"><strong>{{ item.productName }}</strong><small v-if="item.variationName">{{ item.variationName }}</small>
                    <small v-for="group in item.modifierGroups ?? []" :key="group.groupId">{{ group.name }}: {{ group.options.map(option => option.name).join(', ') }}</small>
                    <small v-for="additional in item.additionals" :key="additional.name">+ {{ additional.name }}<template v-if="additional.quantity > 1"> ({{ additional.quantity }}×)</template></small>
                  </span>
                  <strong class="tracking-item-price">{{ money(item.total) }}</strong>
                </li>
              </ul>
              <div class="tracking-totals">
                <div><span>Subtotal</span><span>{{ money(order.subtotal) }}</span></div>
                <div v-if="order.discount > 0"><span>Desconto</span><span>− {{ money(order.discount) }}</span></div>
                <div v-if="order.fees > 0"><span>Taxas</span><span>{{ money(order.fees) }}</span></div>
                <div v-if="order.serviceType === 'Delivery' && order.deliveryFee != null"><span>Entrega</span><span>{{ money(order.deliveryFee) }}</span></div>
                <div class="tracking-total"><strong>Total do pedido</strong><strong>{{ money(order.total) }}</strong></div>
              </div>
            </section>

            <section v-if="canCancel" class="tracking-card cancel-panel">
              <div class="tracking-card__heading"><div><p class="tracking-eyebrow">PRECISA DE AJUDA?</p><h3>Cancelar pedido</h3></div><q-icon name="support_agent" size="24px" aria-hidden="true" /></div>
              <p>Se necessário, você pode solicitar o cancelamento enquanto o pedido ainda está sendo confirmado.</p>
              <q-input v-model="reason" label="Motivo (opcional)" outlined dense />
              <q-btn class="tracking-cancel-button" outline color="negative" icon="cancel" label="Cancelar pedido"
                :loading="cancelling" :disable="cancelling" @click="cancel" />
            </section>
            <section v-else-if="!terminalOrderStatuses.has(order.status)" class="tracking-help-card">
              <q-icon name="info" aria-hidden="true" />
              <p>O cancelamento pelo cliente não está mais disponível. Continue acompanhando por aqui.</p>
            </section>
          </aside>
        </div>
      </template>
    </main>
  </q-page>
</template>

<style scoped>
.tracking-panel { width: min(100%, 1040px); padding: clamp(20px, 4vw, 40px); }
.tracking-heading, .tracking-card__heading { display: flex; align-items: center; justify-content: space-between; gap: 16px; }
.tracking-heading { margin-bottom: 28px; }
.tracking-heading > .q-icon, .tracking-card__heading > .q-icon { color: var(--zf-brand-primary-text); }
.tracking-home-button { min-height: 44px; color: var(--zf-brand-primary-text); }
.tracking-heading h1 { color: var(--zf-text-primary); font-size: clamp(1.8rem, 4vw, 2.5rem); line-height: 1.12; }
.tracking-orders { margin: 0 0 24px; padding: 14px; border: 1px solid var(--zf-border-subtle); border-radius: var(--zf-border-radius); background: var(--zf-surface-raised); }
.tracking-orders__list { display: flex; flex-wrap: wrap; gap: 10px; }
.tracking-order-link { display: grid; gap: 2px; min-width: 170px; padding: 10px 12px; border: 1px solid var(--zf-border-subtle); border-radius: 10px; color: var(--zf-text-primary); text-decoration: none; }
.tracking-order-link:hover, .tracking-order-link--active { border-color: var(--zf-brand-primary); background: var(--zf-brand-soft); }
.tracking-order-link span, .tracking-order-link small { color: var(--zf-text-muted); }
.tracking-eyebrow { margin: 0 0 5px; color: var(--zf-brand-primary-text); font-size: .72rem; font-weight: 800; letter-spacing: .12em; }
.tracking-loading { display: flex; align-items: center; justify-content: center; gap: 12px; min-height: 220px; color: var(--zf-text-muted); }
.tracking-status-card { display: flex; align-items: center; gap: 18px; margin: 24px 0; padding: 22px; border: 1px solid var(--zf-border-subtle); border-radius: var(--zf-border-radius); background: color-mix(in srgb, var(--zf-brand-soft) 42%, var(--zf-surface-raised)); }
.tracking-status-card__icon { display: grid; flex: 0 0 54px; width: 54px; height: 54px; place-items: center; border-radius: 50%; background: var(--zf-surface-raised); }
.tracking-status-card__icon--confirmed, .tracking-status-card__icon--info { color: var(--zf-status-info); }
.tracking-status-card__icon--preparing { color: var(--zf-status-warning-text); }
.tracking-status-card__icon--ready, .tracking-status-card__icon--success { color: var(--zf-status-success-text); }
.tracking-status-card__icon--danger { color: var(--zf-status-danger); }
.tracking-status-card__copy { min-width: 0; flex: 1; }
.tracking-status-card__copy > span { color: var(--zf-text-muted); font-size: .88rem; font-weight: 700; }
.tracking-status-card__copy h2 { margin: 2px 0 4px; font-size: clamp(1.45rem, 3vw, 2rem); }
.tracking-status-card__copy p { display: flex; align-items: center; gap: 5px; margin: 0; color: var(--zf-text-muted); }
.tracking-status-card__copy .tracking-scheduled { margin-top: 7px; color: var(--zf-brand-primary-text); }
.tracking-live { display: inline-flex; align-items: center; gap: 7px; align-self: flex-start; padding: 6px 10px; border-radius: 999px; background: var(--zf-brand-soft); color: var(--zf-brand-primary-text); font-size: .76rem; font-weight: 700; white-space: nowrap; }
.tracking-live > span { width: 8px; height: 8px; border-radius: 50%; background: var(--zf-status-success); }
.tracking-grid { display: grid; grid-template-columns: minmax(0, 1.15fr) minmax(300px, .85fr); align-items: start; gap: 18px; }
.tracking-side-column { display: grid; gap: 18px; }
.tracking-card { min-width: 0; padding: 20px; border: 1px solid var(--zf-border-subtle); border-radius: var(--zf-border-radius); background: var(--zf-surface-raised); }
.tracking-card__heading { margin-bottom: 18px; }
.tracking-card__heading h3 { margin: 0; color: var(--zf-text-primary); font-size: 1.1rem; }
.timeline { display: grid; gap: 0; margin: 0; padding: 0; list-style: none; }
.timeline li { position: relative; display: grid; grid-template-columns: 28px minmax(0, 1fr); gap: 12px; min-height: 76px; }
.timeline li:not(:last-child)::after { position: absolute; top: 27px; bottom: 0; left: 13px; width: 2px; background: var(--zf-border-subtle); content: ''; }
.timeline__marker { z-index: 1; display: grid; width: 28px; height: 28px; place-items: center; border: 1px solid var(--zf-border-subtle); border-radius: 50%; background: var(--zf-surface-raised); color: var(--zf-text-muted); }
.timeline__item--latest .timeline__marker { border-color: var(--zf-brand-primary); background: var(--zf-brand-primary); color: var(--zf-brand-on-primary); }
.timeline__content { display: grid; align-content: start; gap: 3px; padding: 3px 0 18px; }
.timeline__content time, .timeline__content > span { color: var(--zf-text-muted); font-size: .84rem; }
.tracking-status--confirmed, .tracking-status--info { color: var(--zf-status-info); }
.tracking-status--preparing { color: var(--zf-status-warning-text); }
.tracking-status--ready, .tracking-status--success { color: var(--zf-status-success-text); }
.tracking-status--danger { color: var(--zf-status-danger); }
.tracking-items { display: grid; gap: 14px; margin: 0; padding: 0 0 18px; border-bottom: 1px solid var(--zf-border-subtle); list-style: none; }
.tracking-items li { display: grid; grid-template-columns: auto minmax(0, 1fr) auto; align-items: start; gap: 10px; }
.tracking-item-quantity { display: grid; min-width: 34px; min-height: 28px; place-items: center; border-radius: 8px; background: var(--zf-brand-soft); color: var(--zf-brand-primary-text); font-size: .82rem; font-weight: 800; }
.tracking-item-copy { display: grid; gap: 3px; min-width: 0; }
.tracking-item-copy small { color: var(--zf-text-muted); line-height: 1.35; }
.tracking-item-price { font-size: .88rem; white-space: nowrap; }
.tracking-totals { display: grid; gap: 10px; padding-top: 16px; }
.tracking-totals > div { display: flex; justify-content: space-between; gap: 12px; color: var(--zf-text-muted); font-size: .9rem; }
.tracking-totals .tracking-total { margin-top: 4px; padding-top: 12px; border-top: 1px solid var(--zf-border-subtle); color: var(--zf-text-primary); font-size: 1rem; }
.cancel-panel { display: grid; gap: 12px; margin: 0; padding: 20px; border: 1px solid var(--zf-border-subtle); border-radius: var(--zf-border-radius); }
.cancel-panel .tracking-card__heading { margin-bottom: 0; }
.cancel-panel > p { margin: 0; color: var(--zf-text-muted); font-size: .9rem; }
.tracking-cancel-button { min-height: 44px; justify-self: start; }
.tracking-help-card { display: flex; align-items: flex-start; gap: 10px; padding: 16px; border-radius: var(--zf-border-radius); background: var(--zf-brand-soft); color: var(--zf-brand-primary-text); }
.tracking-help-card p { margin: 0; }
@media (max-width: 700px) {
  .tracking-panel { padding: 18px; }
  .tracking-heading { margin-bottom: 20px; }
  .tracking-home-button { align-self: flex-start; }
  .tracking-status-card { align-items: flex-start; gap: 12px; padding: 16px; }
  .tracking-status-card__icon { flex-basis: 42px; width: 42px; height: 42px; }
  .tracking-status-card__icon :deep(.q-icon) { font-size: 25px !important; }
  .tracking-live { padding: 5px 8px; font-size: .7rem; }
  .tracking-grid { grid-template-columns: minmax(0, 1fr); }
}
@media (max-width: 420px) {
  .tracking-status-card { display: grid; grid-template-columns: 42px minmax(0, 1fr); }
  .tracking-live { grid-column: 2; justify-self: start; }
}
</style>
