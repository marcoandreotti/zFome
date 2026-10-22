<script setup lang="ts">
import type { Category, Product, ReusableItem } from './client'

defineProps<{
  rows: (Category | Product | ReusableItem)[]
  loading: boolean
}>()

const emit = defineEmits<{
  edit: [row: Category | Product | ReusableItem]
}>()
</script>

<template>
  <q-markup-table flat bordered wrap-cells :aria-busy="loading">
    <thead>
      <tr>
        <th class="text-left">Nome</th>
        <th>Estado</th>
        <th>Ações</th>
      </tr>
    </thead>
    <tbody>
      <tr v-for="row in rows" :key="row.id">
        <td>{{ row.name }}</td>
        <td class="text-center">{{ row.isActive ? 'Ativo' : 'Inativo' }}</td>
        <td class="text-center">
          <q-btn flat
            square
            class="collection-action-btn"
            color="primary"
            icon="edit"
            :aria-label="`Editar ${row.name}`"
            :disable="loading"
            @click="emit('edit', row)"
          ><q-tooltip>Editar {{ row.name }}</q-tooltip></q-btn>
        </td>
      </tr>
    </tbody>
  </q-markup-table>
</template>
