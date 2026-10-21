<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useRoute } from 'vue-router'
import ProblemBanner from '../../components/ProblemBanner.vue'
import { receiptStorage } from './cart'
import { publicOrderingClient, type ReviewEligibility } from './client'

const reference = String(useRoute().params.reference)
const eligibility = ref<ReviewEligibility>()
const menuSlug = ref(receiptStorage.findSlug(reference))
const trackingPath = `/order/track/${encodeURIComponent(reference)}`
const menuPath = ref(menuSlug.value ? `/order/${encodeURIComponent(menuSlug.value)}` : null)
const loading = ref(true)
const sending = ref(false)
const sent = ref(false)
const error = ref<unknown>(null)
const establishmentRating = ref(0)
const establishmentComment = ref('')
const productRatings = ref<Record<string, number>>({})
const productComments = ref<Record<string, string>>({})
const submissionKey = ref<{ signature: string; key: string } | undefined>((() => {
  try {
    const storageKey = `review-submission:${reference}`
    const saved = sessionStorage.getItem(storageKey)
    if (!saved) return undefined
    const parsed = JSON.parse(saved) as { signature?: unknown; key?: unknown }
    if (typeof parsed.signature === 'string' && typeof parsed.key === 'string')
      return { signature: parsed.signature, key: parsed.key }
    sessionStorage.removeItem(storageKey)
    return undefined
  } catch { return undefined }
})())
const ratingLabel = (rating: number) => rating > 0 ? `${rating} de 5 estrelas selecionadas` : 'Nenhuma nota selecionada'
const payloadSignature = async (payload: unknown) => {
  const bytes = new Uint8Array(await crypto.subtle.digest('SHA-256', new TextEncoder().encode(JSON.stringify(payload))))
  return [...bytes].map(value => value.toString(16).padStart(2, '0')).join('')
}
const load = async () => {
  loading.value = true
  try {
    const progress = await publicOrderingClient.reviewProgress(reference)
    const slug = progress.establishmentSlug || menuSlug.value
    if (slug) {
      menuSlug.value = slug
      menuPath.value = `/order/${encodeURIComponent(slug)}`
      try {
        const context = await publicOrderingClient.context(slug)
        if (!progress.establishmentName || progress.establishmentName === 'Estabelecimento')
          progress.establishmentName = context.establishmentName
        progress.establishmentLogoUrl = context.theme.logoUrl
        progress.establishmentSlug ||= context.slug
      } catch { /* A marca da resposta de reviews continua como fallback. */ }
    }
    eligibility.value = progress
    error.value = null
  }
  catch (failure) { error.value = failure }
  finally { loading.value = false }
}
const submit = async () => {
  if (!eligibility.value?.canReview || establishmentRating.value < 1 || sending.value) return
  sending.value = true
  try {
    const payload = {
      establishmentRating: establishmentRating.value,
      establishmentComment: establishmentComment.value.trim() || null,
      products: eligibility.value.products.filter(p => !p.reviewed && productRatings.value[p.productId] > 0)
        .map(p => ({ productId: p.productId, rating: productRatings.value[p.productId], comment: productComments.value[p.productId]?.trim() || null }))
    }
    const signature = await payloadSignature(payload)
    if (submissionKey.value?.signature !== signature)
      submissionKey.value = { signature, key: crypto.randomUUID() }
    sessionStorage.setItem(`review-submission:${reference}`, JSON.stringify(submissionKey.value))
    await publicOrderingClient.submitReviews(reference, payload, submissionKey.value.key)
    sessionStorage.removeItem(`review-submission:${reference}`)
    sent.value = true
  } catch (failure) { error.value = failure }
  finally { sending.value = false }
}
onMounted(load)
</script>

<template>
  <q-page class="ordering-page">
    <main class="review-layout">
      <header class="review-establishment-hero ordering-hero">
        <img v-if="eligibility?.establishmentLogoUrl" :src="eligibility.establishmentLogoUrl" alt="" class="ordering-logo">
        <span v-else class="ordering-unit-mark" aria-hidden="true"><q-icon name="storefront" /></span>
        <div class="ordering-context">
          <p class="ordering-unit-label">Avalia&ccedil;&atilde;o da unidade</p>
          <h1>{{ eligibility?.establishmentName || 'Estabelecimento' }}</h1>
          <p>Sua opini&atilde;o ajuda a melhorar a experi&ecirc;ncia de todos.</p>
        </div>
      </header>
      <section class="review-content">
        <nav class="review-navigation" aria-label="Navega&ccedil;&atilde;o da avalia&ccedil;&atilde;o">
          <q-btn v-if="menuPath" flat icon="arrow_back" label="Voltar ao card&aacute;pio" :to="menuPath" />
          <q-btn flat label="Cancelar avalia&ccedil;&atilde;o" :to="trackingPath" />
        </nav>
        <header class="review-intro">
          <h2>Avalie seu pedido</h2>
          <p>Conte como foi sua experi&ecirc;ncia. Seu coment&aacute;rio ser&aacute; exibido sem identificar voc&ecirc;.</p>
        </header>
        <ProblemBanner :error="error"><q-btn flat label="Tentar novamente" @click="load" /></ProblemBanner>
        <div v-if="loading" class="review-loading" role="status"><q-spinner color="primary" /> Carregando pedido&hellip;</div>
        <q-banner v-else-if="sent" class="bg-positive text-white" rounded role="status">Obrigado! Sua avalia&ccedil;&atilde;o foi recebida e ser&aacute; publicada ap&oacute;s an&aacute;lise da equipe.</q-banner>
        <q-banner v-else-if="eligibility?.pendingApproval" class="bg-orange-1 text-dark" rounded role="status">Sua avalia&ccedil;&atilde;o est&aacute; aguardando an&aacute;lise da equipe antes de aparecer publicamente.</q-banner>
        <q-banner v-else-if="eligibility && !eligibility.canReview" class="bg-grey-2" rounded>
          Este pedido n&atilde;o est&aacute; mais dispon&iacute;vel para avalia&ccedil;&atilde;o.
        </q-banner>
        <q-form v-else-if="eligibility" class="review-form" @submit.prevent="submit">
          <section class="review-target review-target--establishment" aria-labelledby="establishment-review-title">
            <h3 id="establishment-review-title">Como foi a experi&ecirc;ncia com o estabelecimento?</h3>
            <q-rating v-model="establishmentRating" :max="5" size="2.5em" color="amber" icon="star_border" icon-selected="star" :icon-aria-label="['1 estrela','2 estrelas','3 estrelas','4 estrelas','5 estrelas']" aria-label="Nota do estabelecimento" />
            <span aria-live="polite">{{ ratingLabel(establishmentRating) }}</span>
            <label for="establishment-comment">Coment&aacute;rio <span>(opcional; obrigat&oacute;rio para notas abaixo de 3)</span></label>
            <q-input id="establishment-comment" v-model="establishmentComment" type="textarea" autogrow maxlength="1000" counter outlined :rules="[v => establishmentRating >= 3 || String(v ?? '').trim().length >= 10 || 'Para notas abaixo de 3, escreva ao menos 10 caracteres']" />
          </section>
          <section v-for="product in eligibility.products.filter(p => !p.reviewed)" :key="product.productId" class="review-target">
            <h3>{{ product.productName }}</h3>
            <p>Opcional</p>
            <q-rating v-model="productRatings[product.productId]" :max="5" size="2em" color="amber" icon="star_border" icon-selected="star" :icon-aria-label="['1 estrela','2 estrelas','3 estrelas','4 estrelas','5 estrelas']" :aria-label="`Nota para ${product.productName}`" />
            <span aria-live="polite">{{ ratingLabel(productRatings[product.productId] || 0) }}</span>
            <label :for="`comment-${product.productId}`">Coment&aacute;rio <span>(obrigat&oacute;rio para notas abaixo de 3)</span></label>
            <q-input :id="`comment-${product.productId}`" v-model="productComments[product.productId]" type="textarea" autogrow maxlength="1000" counter outlined :rules="[v => !productRatings[product.productId] || productRatings[product.productId] >= 3 || String(v ?? '').trim().length >= 10 || 'Para notas abaixo de 3, escreva ao menos 10 caracteres']" />
          </section>
          <q-btn type="submit" color="primary" label="Enviar avalia&ccedil;&atilde;o" :loading="sending" :disable="establishmentRating < 1" />
        </q-form>
      </section>
    </main>
  </q-page>
</template>

<style scoped>
.review-layout { width: min(100%, 1100px); margin: 20px auto 36px; }
.review-establishment-hero { align-items: center; width: 100%; min-height: 190px; margin: 0 auto 24px; padding: clamp(22px, 4vw, 40px); border: 1px solid var(--zf-border-subtle); border-radius: var(--zf-border-radius); background: color-mix(in srgb, var(--zf-brand-primary) 8%, var(--zf-surface-raised)); }
.review-establishment-hero .ordering-context h1 { max-width: 100%; overflow-wrap: anywhere; font-size: clamp(2rem, 5vw, 3.5rem); line-height: 1.08; letter-spacing: -.025em; }
.review-establishment-hero .ordering-context > p:last-child { margin: 10px 0 0; color: var(--zf-text-muted); font-size: 1.05rem; }
.review-establishment-hero .ordering-logo, .review-establishment-hero .ordering-unit-mark { flex: 0 0 clamp(64px, 10vw, 96px); width: clamp(64px, 10vw, 96px); height: clamp(64px, 10vw, 96px); padding: 8px; border: 1px solid var(--zf-border-subtle); border-radius: 18px; background: var(--zf-surface-raised); }
.review-establishment-hero .ordering-logo { object-fit: contain; }
.review-establishment-hero .ordering-unit-mark { display: grid; place-items: center; color: var(--zf-brand-primary-text); font-size: 42px; }
.review-establishment-hero .ordering-unit-label { margin: 0 0 7px; color: var(--zf-brand-primary-text); font-size: .78rem; font-weight: 750; letter-spacing: .045em; }
.review-content { width: min(100%, 760px); margin: 0 auto; padding: clamp(20px, 4vw, 32px) 0; }
.review-navigation { display: flex; flex-wrap: wrap; gap: 8px; margin-bottom: 24px; }
.review-intro { margin-bottom: 26px; }
.review-intro h2 { margin: 0; color: var(--zf-text-primary); font-size: clamp(1.7rem, 4vw, 2.25rem); font-weight: 750; letter-spacing: -.03em; line-height: 1.12; }
.review-intro p { max-width: 58ch; margin: 10px 0 0; color: var(--zf-text-muted); line-height: 1.6; }
.review-loading { display: flex; align-items: center; gap: 10px; min-height: 150px; color: var(--zf-text-muted); }
.review-form { display: grid; gap: 8px; margin-top: 12px; }
.review-target { display: grid; gap: 12px; padding: 24px 0; border-bottom: 1px solid var(--zf-border-subtle); }
.review-target--establishment { padding-top: 4px; }
.review-target h3 { max-width: 52ch; margin: 0; color: var(--zf-text-primary); font-size: clamp(1.08rem, 2.5vw, 1.3rem); line-height: 1.35; }
.review-target > p, .review-target > span { margin: 0; color: var(--zf-text-muted); font-size: .9rem; }
.review-target > label { margin-top: 4px; color: var(--zf-text-primary); font-weight: 600; }
.review-target label span { color: var(--zf-text-muted); font-weight: 400; }
.review-target :deep(.q-rating) { justify-self: start; }
.review-content :deep(.q-btn[type='submit']) { min-height: 48px; margin-top: 18px; justify-self: start; padding-inline: 24px; }
@media (max-width: 700px) {
  .review-layout { margin-top: 12px; padding-inline: 12px; }
  .review-establishment-hero { min-height: 0; gap: 16px; padding: 22px 18px; }
  .review-establishment-hero .ordering-context { flex: 1 1 180px; }
  .review-content { padding-top: 24px; }
}
@media (max-width: 420px) {
  .review-establishment-hero .ordering-context h1 { font-size: 2rem; }
}
</style>
