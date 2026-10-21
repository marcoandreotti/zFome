<script setup lang="ts">
defineProps<{ modelValue: string }>()
defineEmits<{ 'update:modelValue': [value: string] }>()
</script>

<template>
  <label class="catalog-search">
    <span>Buscar no cardápio</span>
    <input
      type="search"
      :value="modelValue"
      placeholder="Produto ou descrição"
      @input="$emit('update:modelValue', ($event.target as HTMLInputElement).value)"
    >
  </label>
</template>
