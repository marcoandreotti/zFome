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
    <span class="product-card__media">
      <img v-if="image" class="product-card__image" :src="image.url" alt="">
      <span v-else class="product-card__placeholder" aria-hidden="true"><q-icon name="restaurant" /></span>
      <span v-if="product.ratingCount" class="product-card__rating" :aria-label="`Nota ${Number(product.ratingAverage).toLocaleString('pt-BR', { maximumFractionDigits: 1 })} de 5, ${product.ratingCount} avaliações`">
        <q-icon name="star" aria-hidden="true" /> {{ Number(product.ratingAverage).toLocaleString('pt-BR', { maximumFractionDigits: 1 }) }} <small>({{ product.ratingCount }})</small>
      </span>
    </span>
    <span class="product-card__body">
      <strong class="product-card__name">{{ product.name }}</strong>
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
  min-height: 167px;
  align-items: stretch;
  padding: 0;
  border: 1px solid var(--zf-border-subtle);
  border-radius: var(--zf-border-radius);
  overflow: hidden;
  background: var(--zf-surface-raised);
  color: var(--zf-text-primary);
  text-align: left;
  cursor: pointer;
  transition: border-color 150ms ease, transform 150ms ease;
}
.product-card:hover:not(:disabled) { border-color: var(--zf-brand-primary); transform: translateY(-2px); }
.product-card:focus-visible { outline: 3px solid var(--zf-focus-ring); outline-offset: 3px; }
.product-card:disabled { cursor: not-allowed; opacity: .72; }
.product-card__media {
  position: relative;
  display: flex;
  flex: 0 0 112px;
  width: 112px;
  height: 112px;
  min-height: 167px;
  min-width: 112px;
  box-sizing: border-box;
  margin-left: -1px;
  padding: 0;
  gap: 0;
  aspect-ratio: 1;
  align-self: auto;
  overflow: hidden;
}
.product-card__image, .product-card__placeholder {
  width: 100%;
  height: 100%;
  object-fit: cover;
}
.product-card__placeholder { display: grid; place-items: center; background: color-mix(in srgb, var(--zf-brand-primary) 9%, var(--zf-surface-raised)); color: var(--zf-brand-primary-text); font-size: 42px; }
.product-card__body { display: flex; min-width: 0; flex: 1; flex-direction: column; justify-content: center; gap: 5px; overflow: hidden; padding: 14px; }
.product-card__name { color: var(--zf-text-primary); font-size: 1.05rem; line-height: 1.25; overflow-wrap: anywhere; }
.product-card__rating {
  position: absolute;
  right: 0;
  bottom: 0;
  left: 0;
  display: flex;
  min-height: 28px;
  align-items: center;
  justify-content: center;
  gap: 4px;
  padding: 4px 7px;
  background: rgb(17 24 39 / 72%);
  color: #fff;
  font-size: .78rem;
  font-weight: 700;
}
.product-card__rating :deep(.q-icon) { color: var(--zf-status-warning); }
.product-card__rating small { color: rgb(255 255 255 / 82%); font-size: .72rem; font-weight: 600; }
.product-card__description { display: -webkit-box; overflow: hidden; color: var(--zf-text-muted); font-size: .88rem; line-height: 1.3; overflow-wrap: anywhere; -webkit-box-orient: vertical; -webkit-line-clamp: 2; }
.product-card__footer { display: flex; align-items: center; justify-content: space-between; gap: 10px; margin-top: auto; }
.product-card__footer b { color: var(--zf-brand-primary-text); font-size: .92rem; line-height: 1.3; }
.product-card__add { flex: 0 0 auto; color: var(--zf-brand-primary-text); font-size: 26px; }
@media (max-width: 700px) {
  .product-card { height: 132px; }
  .product-card__media { min-height: 112px; }
  .product-card__body { padding: 14px; }
}
@media (prefers-reduced-motion: reduce) {
  .product-card { transition: none; }
}
</style>
