<script setup lang="ts">
import { computed } from 'vue'
import type { Product } from './types'

const props = defineProps<{
  product: Product
  formattedPrice: string
}>()

defineEmits<{ select: [product: Product] }>()

const image = computed(() => [...props.product.images].sort((a, b) => a.order - b.order)[0])
</script>

<template>
  <button
    class="product-card"
    type="button"
    :disabled="product.isAvailable === false"
    :aria-label="`${product.name}${product.isAvailable === false ? ', indisponível' : `, a partir de ${formattedPrice}. Toque para personalizar`}`"
    @click="$emit('select', product)"
  >
    <img v-if="image" class="product-card__image" :src="image.url" alt="">
    <span v-else class="product-card__placeholder" aria-hidden="true"><q-icon name="restaurant" /></span>
    <span class="product-card__body">
      <strong class="product-card__name">{{ product.name }}</strong>
      <span v-if="product.ratingCount" class="product-card__rating" :aria-label="`Nota ${Number(product.ratingAverage).toLocaleString('pt-BR', { maximumFractionDigits: 1 })} de 5, ${product.ratingCount} avaliações`">
        <q-icon name="star" aria-hidden="true" /> {{ Number(product.ratingAverage).toLocaleString('pt-BR', { maximumFractionDigits: 1 }) }} <small>({{ product.ratingCount }})</small>
      </span>
      <small v-if="product.description" class="product-card__description">{{ product.description }}</small>
      <span class="product-card__footer">
        <b v-if="product.isAvailable !== false">A partir de {{ formattedPrice }}</b>
        <b v-else>
        Indisponível<span v-if="product.unavailabilityReason"> · {{ product.unavailabilityReason }}</span>
        </b>
        <q-icon v-if="product.isAvailable !== false" name="add_circle" class="product-card__add" aria-hidden="true" />
      </span>
    </span>
  </button>
</template>

<style scoped>
.product-card {
  display: flex;
  width: 100%;
  min-width: 0;
  min-height: 168px;
  align-items: stretch;
  padding: 0;
  border: 1px solid var(--oh-border-subtle);
  border-radius: var(--oh-border-radius);
  overflow: hidden;
  background: var(--oh-surface-raised);
  color: var(--oh-text-primary);
  text-align: left;
  cursor: pointer;
  transition: border-color 150ms ease, transform 150ms ease;
}
.product-card:hover:not(:disabled) { border-color: var(--oh-brand-primary); transform: translateY(-2px); }
.product-card:focus-visible { outline: 3px solid var(--oh-focus-ring); outline-offset: 3px; }
.product-card:disabled { cursor: not-allowed; opacity: .72; }
.product-card__image, .product-card__placeholder {
  flex: 0 0 clamp(112px, 34%, 168px);
  width: clamp(112px, 34%, 168px);
  min-width: 112px;
  aspect-ratio: 1;
  align-self: center;
  object-fit: cover;
}
.product-card__placeholder { display: grid; place-items: center; background: color-mix(in srgb, var(--oh-brand-primary) 9%, var(--oh-surface-raised)); color: var(--oh-brand-primary-text); font-size: 42px; }
.product-card__body { display: flex; min-width: 0; flex: 1; flex-direction: column; justify-content: center; gap: 7px; padding: 16px; }
.product-card__name { color: var(--oh-text-primary); font-size: 1.05rem; line-height: 1.25; overflow-wrap: anywhere; }
.product-card__rating { display: flex; align-items: center; gap: 4px; color: var(--oh-text-muted); font-size: .84rem; }
.product-card__rating :deep(.q-icon) { color: #b77900; }
.product-card__rating small { font-size: .78rem; }
.product-card__description { display: -webkit-box; overflow: hidden; color: var(--oh-text-muted); font-size: .88rem; line-height: 1.4; overflow-wrap: anywhere; -webkit-box-orient: vertical; -webkit-line-clamp: 3; }
.product-card__footer { display: flex; align-items: center; justify-content: space-between; gap: 10px; margin-top: 3px; }
.product-card__footer b { color: var(--oh-brand-primary-text); font-size: .92rem; line-height: 1.3; }
.product-card__add { flex: 0 0 auto; color: var(--oh-brand-primary-text); font-size: 26px; }
@media (max-width: 700px) {
  .product-card { min-height: 132px; }
  .product-card__image, .product-card__placeholder { flex-basis: 112px; width: 112px; }
  .product-card__body { padding: 14px; }
}
@media (prefers-reduced-motion: reduce) {
  .product-card { transition: none; }
}
</style>
