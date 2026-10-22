<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref } from 'vue'
import { isCancel } from 'axios'
import { ApiError } from '../../../http/client'
import ProblemBanner from '../../../components/ProblemBanner.vue'
import { useSessionStore } from '../../session/store'
import { roles, usersClient, type AdministrativeUser } from './client'
import UserPermissions from './UserPermissions.vue'
const session = useSessionStore()
const items = ref<AdministrativeUser[]>([])
const total = ref(0),
  page = ref(1),
  search = ref('')
const associatedOnly = ref(false)
const state = ref('all')
const loading = ref(false),
  busy = ref(false),
  editor = ref(false)
const error = ref<unknown>(null),
  editError = ref<unknown>(null)
const selected = ref<AdministrativeUser | null>(null)
const name = ref(''),
  email = ref(''),
  password = ref(''),
  initialRole = ref(3)
const notification = ref('')
const confirmation = ref<{ message: string; run: () => Promise<void> } | null>(
  null
)
const confirmationButton = computed(() => {
  const message = confirmation.value?.message ?? ''
  if (message.startsWith('Conceder')) return { label: 'Conceder acesso', icon: 'person_add', color: 'positive' }
  if (message.startsWith('Remover')) return { label: 'Remover acesso', icon: 'person_remove', color: 'negative' }
  if (message.startsWith('Ativar')) return { label: 'Ativar usuário', icon: 'toggle_on', color: 'positive' }
  if (message.startsWith('Desativar')) return { label: 'Desativar usuário', icon: 'toggle_off', color: 'negative' }
  if (message.startsWith('Revogar')) return { label: 'Revogar acesso', icon: 'block', color: 'negative' }
  return { label: 'Confirmar', icon: 'check', color: 'primary' }
})
const pageCount = computed(() => Math.max(1, Math.ceil(total.value / 20)))
const availableRoles = computed(() =>
  roles.filter((role) => role.value !== 1 || session.can('ownership'))
)
const required = (value: string) => !!value.trim() || 'Campo obrigatório'
const field = (key: string) =>
  editError.value instanceof ApiError ? editError.value.field(key) : undefined
let request: AbortController | undefined
let mounted = true
async function load() {
  if (!session.unitId) return
  request?.abort()
  const current = new AbortController()
  request = current
  loading.value = true
  error.value = null
  try {
    const result = await usersClient.search(
      session.unitId,
      {
        search: search.value,
        page: page.value,
        pageSize: 20,
        associatedOnly: associatedOnly.value,
        isActive: state.value === 'all' ? undefined : state.value === 'active'
      },
      current.signal
    )
    if (!mounted || current.signal.aborted) return
    items.value = result.items
    total.value = result.totalCount
    if (selected.value)
      selected.value =
        result.items.find((user) => user.id === selected.value?.id) ??
        selected.value
  } catch (failure) {
    if (!isCancel(failure)) {
      error.value = failure
      items.value = []
    }
  } finally {
    if (request === current) loading.value = false
  }
}
function filter() {
  page.value = 1
  void load()
}
function open(user: AdministrativeUser | null) {
  selected.value = user
  name.value = user?.name ?? ''
  email.value = user?.email ?? ''
  password.value = ''
  initialRole.value = 3
  editError.value = null
  notification.value = ''
  editor.value = true
}
async function save() {
  if (busy.value) return
  busy.value = true
  editError.value = null
  try {
    if (selected.value)
      await usersClient.update(session.unitId, selected.value.id, name.value)
    else
      await usersClient.create(session.unitId, {
        name: name.value,
        email: email.value,
        password: password.value,
        initialRole: initialRole.value
      })
    password.value = ''
    editor.value = false
    notification.value = 'Usuário salvo.'
    await load()
  } catch (failure) {
    editError.value = failure
  } finally {
    busy.value = false
  }
}
function ask(message: string, run: () => Promise<void>) {
  confirmation.value = { message, run }
}
async function confirm() {
  const pending = confirmation.value
  if (!pending || busy.value) return
  busy.value = true
  editError.value = null
  try {
    await pending.run()
    confirmation.value = null
    editor.value = false
    notification.value = 'Alteração concluída.'
    await session.hydrate()
    if (mounted) await load()
  } catch (failure) {
    confirmation.value = null
    editError.value = failure
  } finally {
    busy.value = false
  }
}
function roleChange(role: number, granted: boolean) {
  const user = selected.value
  if (!user) return
  ask(
    `${granted ? 'Conceder' : 'Remover'} ${roles.find((item) => item.value === role)?.label} para ${user.name}?`,
    () => usersClient.role(session.unitId, user.id, role, granted)
  )
}
function activeChange(active: boolean) {
  const user = selected.value
  if (!user) return
  ask(`${active ? 'Ativar' : 'Desativar'} ${user.name}?`, () =>
    usersClient.active(session.unitId, user.id, active)
  )
}
function accessChange(granted: boolean) {
  const user = selected.value
  if (!user) return
  ask(
    `${granted ? 'Conceder' : 'Revogar'} acesso de ${user.name} à unidade selecionada?`,
    () => usersClient.access(session.unitId, user.id, granted)
  )
}
onMounted(load)
onUnmounted(() => {
  mounted = false
  request?.abort()
  password.value = ''
  confirmation.value = null
})
</script>
<template>
  <q-page class="admin-page">
    <header class="admin-page-header">
      <div>
        <p class="admin-page-eyebrow">ACESSOS</p>
        <h1 class="text-h4 q-my-sm">Usuários</h1>
        <p>
          Pessoas do Tenant, papéis e acesso à unidade selecionada.
        </p>
      </div>
      <q-btn
        color="primary"
        no-caps
        label="Novo usuário"
        :disable="!session.unitId"
        @click="open(null)"
      />
    </header>
    <q-banner v-if="!session.unitId" class="bg-amber-1"
      >Selecione uma unidade autorizada.</q-banner
    >
    <p v-if="notification" role="status" class="text-positive">
      {{ notification }}
    </p>
    <ProblemBanner :error="error"
      ><q-btn flat label="Tentar novamente" @click="load"
    /></ProblemBanner>
    <q-form class="row q-col-gutter-md items-start q-mb-md" @submit="filter">
      <q-input
        v-model="search"
        class="col-12 col-md-5"
        outlined
        label="Pesquisar nome ou e-mail"
        maxlength="150"
      />
      <q-select
        v-model="state"
        class="col-12 col-md-3"
        outlined
        label="Estado"
        emit-value
        map-options
        :options="[
          { value: 'all', label: 'Todos' },
          { value: 'active', label: 'Ativos' },
          { value: 'inactive', label: 'Inativos' }
        ]"
      />
      <div class="col-12 col-md-4">
        <q-checkbox
          v-model="associatedOnly"
          label="Somente associados à unidade"
        /><q-btn type="submit" flat label="Pesquisar" :loading="loading" />
      </div>
    </q-form>
    <q-markup-table flat bordered wrap-cells :aria-busy="loading">
      <thead>
        <tr>
          <th class="text-left">Usuário</th>
          <th class="text-left">Papéis</th>
          <th class="text-left">Estado</th>
          <th>Ações</th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="user in items" :key="user.id">
          <td>
            <strong>{{ user.name }}</strong
            ><span v-if="user.isCurrentUser"> (você)</span>
            <div>{{ user.email }}</div>
          </td>
          <td>
            {{
              roles
                .filter((role) => user.roles.includes(role.value))
                .map((role) => role.label)
                .join(', ')
            }}
          </td>
          <td>
            {{ user.isActive ? 'Ativo' : 'Inativo' }}
            <div class="text-caption">
              {{
                user.establishmentIds.includes(session.unitId)
                  ? 'Associado à unidade'
                  : 'Sem acesso à unidade'
              }}
            </div>
          </td>
          <td>
            <q-btn flat
              square
              class="collection-action-btn"
              color="primary"
              icon="manage_accounts"
              :aria-label="`Gerenciar ${user.name}`"
              :disable="loading"
              @click="open(user)"
            ><q-tooltip>Gerenciar {{ user.name }}</q-tooltip></q-btn>
          </td>
        </tr>
      </tbody>
    </q-markup-table>
    <div v-if="loading" role="status" class="q-pa-md">
      <q-spinner aria-hidden="true" /> Carregando usuários…
    </div>
    <p v-else-if="!items.length && !error" role="status">
      Nenhum usuário encontrado para os filtros informados.
    </p>
    <div class="row items-center justify-between q-mt-md">
      <span>{{ total }} usuários</span
      ><q-pagination
        v-model="page"
        :max="pageCount"
        :max-pages="5"
        :disable="loading"
        @update:model-value="load"
      />
    </div>
    <q-dialog
      v-model="editor"
      :persistent="busy"
      @hide="password = ''"
      aria-label="Gerenciar usuário"
    >
      <q-card style="width: 640px; max-width: 95vw">
        <q-card-section
          ><h2 class="text-h5 q-my-sm">
            {{ selected ? 'Gerenciar usuário' : 'Novo usuário' }}
          </h2>
          <ProblemBanner :error="editError" />
          <q-form class="q-gutter-md" @submit="save">
            <q-input
              v-model="name"
              autofocus
              outlined
              label="Nome"
              maxlength="150"
              :rules="[required]"
              :error="!!field('name')"
              :error-message="field('name')"
              :disable="busy"
            />
            <q-input
              v-model="email"
              outlined
              label="E-mail"
              type="email"
              :readonly="!!selected"
              maxlength="150"
              :rules="[required]"
              :error="!!field('email')"
              :error-message="field('email')"
              :disable="busy"
            />
            <template v-if="!selected"
              ><q-input
                v-model="password"
                outlined
                type="password"
                autocomplete="new-password"
                label="Senha inicial"
                maxlength="200"
                :rules="[
                  (value) => value.length >= 10 || 'Mínimo de 10 caracteres'
                ]"
                :error="!!field('password')"
                :error-message="field('password')"
                :disable="busy"
              /><q-select
                v-model="initialRole"
                outlined
                label="Papel inicial"
                :options="availableRoles"
                emit-value
                map-options
                :disable="busy"
              />
              <p class="text-caption">
                O usuário será associado à unidade selecionada. O acesso exige
                senha e código por e-mail.
              </p></template
            >
            <div class="row justify-end q-gutter-sm">
              <q-btn
                flat
                label="Fechar"
                :disable="busy"
                @click="editor = false"
              /><q-btn
                color="primary"
                icon="save"
                label="Salvar"
                type="submit"
                :loading="busy"
              />
            </div>
          </q-form>
          <UserPermissions
            v-if="selected"
            :user="selected"
            :ownership="session.can('ownership')"
            :platform="!!session.context?.isPlatformUser"
            :unit-id="session.unitId"
            :busy="busy"
            @role="roleChange"
            @active="activeChange"
            @access="accessChange"
          />
        </q-card-section>
      </q-card>
    </q-dialog>
    <q-dialog
      :model-value="!!confirmation"
      persistent
      aria-label="Confirmar alteração do usuário"
      ><q-card style="max-width: 440px"
        ><q-card-section
          ><h2 class="text-h6">Confirmar alteração</h2>
          <p>{{ confirmation?.message }}</p></q-card-section
        ><q-card-actions align="right"
          ><q-btn
            flat
            label="Cancelar"
            :disable="busy"
            @click="confirmation = null" /><q-btn
            :color="confirmationButton.color"
            :icon="confirmationButton.icon"
            :label="confirmationButton.label"
            :loading="busy"
            @click="confirm" /></q-card-actions></q-card
    ></q-dialog>
  </q-page>
</template>
