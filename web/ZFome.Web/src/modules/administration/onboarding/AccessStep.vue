<script setup lang="ts">
import { onMounted, onUnmounted, ref } from 'vue'
import { usersClient, type AdministrativeUser } from '../users/client'
import UserPermissions from '../users/UserPermissions.vue'
import { useSessionStore } from '../../session/store'
import ProblemBanner from '../../../components/ProblemBanner.vue'
const emit = defineEmits<{ changed: [] }>()
const session = useSessionStore()
const users = ref<AdministrativeUser[]>([]), search = ref(''), page = ref(1), total = ref(0)
const busy = ref(false), error = ref<unknown>(null)
let request: AbortController | undefined
let alive = true
async function load() {
  request?.abort(); request = new AbortController(); const current = request
  try {
    const result = await usersClient.search(session.unitId, { search: search.value, page: page.value, pageSize: 20, associatedOnly: false }, current.signal)
    if (alive && !current.signal.aborted) { users.value = result.items; total.value = result.totalCount }
  } catch (e) { if (alive && !current.signal.aborted) error.value = e }
}
async function change(run: () => Promise<void>) {
  if (busy.value) return
  busy.value = true; error.value = null
  try { await run(); await session.hydrate(); if (alive) { await load(); emit('changed') } }
  catch (e) { if (alive) error.value = e }
  finally { busy.value = false }
}
onMounted(load)
onUnmounted(() => { alive = false; request?.abort() })
</script>
<template>
  <section aria-label="Acessos da unidade">
    <p>Associe usuários ativos do grupo. A unidade deve manter pelo menos um Owner ou Admin ativo.</p>
    <ProblemBanner :error="error" />
    <q-form @submit="page = 1; load()" class="row q-gutter-sm q-mb-md">
      <q-input v-model="search" label="Buscar usuários" outlined maxlength="150" />
      <q-btn label="Buscar" type="submit" :disable="busy" />
    </q-form>
    <q-card v-for="user in users" :key="user.id" flat bordered class="q-mb-md">
      <q-card-section>
        <h3 class="text-subtitle1">{{ user.name }} · {{ user.email }}</h3>
        <UserPermissions :user="user" :ownership="session.can('ownership')" :platform="!!session.context?.isPlatformUser" :unit-id="session.unitId" :busy="busy"
          @role="(role, granted) => change(() => usersClient.role(session.unitId, user.id, role, granted))"
          @access="granted => change(() => usersClient.access(session.unitId, user.id, granted))"
          @active="active => change(() => usersClient.active(session.unitId, user.id, active))" />
      </q-card-section>
    </q-card>
    <p v-if="!users.length">Nenhum usuário encontrado.</p>
    <q-pagination v-if="total > 20" v-model="page" :max="Math.ceil(total / 20)" @update:model-value="load" />
    <router-link to="/administration/users">Cadastrar usuários na administração</router-link>
  </section>
</template>
