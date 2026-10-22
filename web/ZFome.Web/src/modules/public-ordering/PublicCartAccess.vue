<script setup lang="ts">
defineProps<{ count: number; total: string; disabled: boolean }>()
defineEmits<{ open: [] }>()
</script>

<template>
  <div class="public-cart-access" aria-live="polite">
    <strong>{{ total }}</strong>
    <button
      type="button"
      class="public-cart-access__button"
      :disabled="disabled"
      @click="$emit('open')"
    >
      Carrinho ({{ count }})
    </button>
  </div>
</template>
