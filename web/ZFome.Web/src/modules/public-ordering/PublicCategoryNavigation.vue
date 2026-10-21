<script setup lang="ts">
import type { PublicCategory } from './types'

defineProps<{
  categories: PublicCategory[]
  modelValue: string | null
}>()

defineEmits<{ 'update:modelValue': [value: string | null] }>()
</script>

<template>
  <nav class="category-navigation" aria-label="Categorias do cardápio">
    <button
      type="button"
      :aria-pressed="modelValue === null"
      @click="$emit('update:modelValue', null)"
    >
      Todos
    </button>
    <button
      v-for="category in categories"
      :key="category.id"
      type="button"
      :aria-pressed="modelValue === category.id"
      @click="$emit('update:modelValue', category.id)"
    >
      {{ category.name }}
    </button>
  </nav>
</template>
