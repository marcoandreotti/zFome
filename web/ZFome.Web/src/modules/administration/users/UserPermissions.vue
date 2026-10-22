<script setup lang="ts">
import { computed } from 'vue'
import { canManageOwner, roles, type AdministrativeUser } from './client'
const props = defineProps<{
  user: AdministrativeUser
  ownership: boolean
  platform: boolean
  unitId: string
  busy: boolean
}>()
defineEmits<{
  role: [role: number, granted: boolean]
  active: [active: boolean]
  access: [granted: boolean]
}>()
const ownerAllowed = computed(() =>
  canManageOwner(props.user, props.ownership, props.platform)
)
</script>
<template>
  <section aria-label="Permissões do usuário">
    <h3 class="text-subtitle1">Papéis</h3>
    <p class="text-caption">
      Owner só pode ser concedido ou removido por outro Owner. As regras são
      verificadas pela API.
    </p>
    <div class="row q-gutter-sm">
      <template v-for="role in roles" :key="role.value">
        <q-checkbox
          v-if="role.value !== 1 || ownerAllowed"
          :model-value="user.roles.includes(role.value)"
          :label="role.label"
          :disable="busy"
          @update:model-value="$emit('role', role.value, !!$event)"
        />
        <q-chip v-else-if="user.roles.includes(1)" outline
          >Owner — protegido</q-chip
        >
      </template>
    </div>
    <h3 class="text-subtitle1">Acesso e estado</h3>
    <q-checkbox
      :model-value="user.establishmentIds.includes(unitId)"
      label="Acesso à unidade selecionada"
      :disable="busy"
      @update:model-value="$emit('access', !!$event)"
    />
    <q-btn
      v-if="!user.roles.includes(1) || ownerAllowed"
      outline
      no-caps
      :icon="user.isActive ? 'toggle_off' : 'toggle_on'"
      :color="user.isActive ? 'negative' : 'positive'"
      :label="user.isActive ? 'Desativar usuário' : 'Ativar usuário'"
      :disable="busy"
      @click="$emit('active', !user.isActive)"
    />
    <p v-else class="text-caption">
      Somente outro Owner pode alterar o estado deste usuário.
    </p>
  </section>
</template>
