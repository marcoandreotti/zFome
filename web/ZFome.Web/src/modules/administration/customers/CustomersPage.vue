<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref } from 'vue'
import { isCancel } from 'axios'
import { ApiError } from '../../../http/client'
import ProblemBanner from '../../../components/ProblemBanner.vue'
import { useSessionStore } from '../../session/store'
import {
  canonicalPhone,
  limitPhone,
  phoneMask,
  phoneValidationMessage
} from '../../../utils/phone'
import { customersClient, type Customer, type Address } from './client'
const session = useSessionStore()
const rows = ref<Customer[]>([]),
  search = ref(''),
  page = ref(1),
  total = ref(0)
const loading = ref(false),
  busy = ref(false),
  error = ref<unknown>(null),
  editError = ref<unknown>(null),
  message = ref('')
const editor = ref(false),
  addresses = ref(false),
  addressEditor = ref(false),
  confirmation = ref<'customer' | 'address' | 'remove' | null>(null)
const customer = ref<Customer>({
  id: '',
  name: '',
  phone: '',
  email: null,
  addresses: []
})
const blankAddress = (): Address => ({
  id: '',
  label: '',
  street: '',
  number: '',
  complement: null,
  neighborhood: '',
  city: '',
  state: '',
  postalCode: '',
  isPrimary: false
})
const address = ref<Address>(blankAddress())
const maxPage = computed(() => Math.max(1, Math.ceil(total.value / 20)))
const fields = [
  { key: 'label', label: 'Rótulo', max: 50 },
  { key: 'street', label: 'Logradouro', max: 200 },
  { key: 'number', label: 'Número', max: 30 },
  { key: 'complement', label: 'Complemento (opcional)', max: 100 },
  { key: 'neighborhood', label: 'Bairro', max: 100 },
  { key: 'city', label: 'Cidade', max: 100 },
  { key: 'state', label: 'UF', max: 2 },
  { key: 'postalCode', label: 'CEP', max: 20 }
] as const
const required = (value: string | null) =>
  !!value?.trim() || 'Campo obrigatório'
const field = (key: string) =>
  editError.value instanceof ApiError ? editError.value.field(key) : undefined
const validPhone = (value: string) => {
  const requiredMessage = required(value)
  return requiredMessage === true ? phoneValidationMessage(value) || true : requiredMessage
}
let request: AbortController | undefined
async function load() {
  if (!session.unitId) return
  request?.abort()
  const current = new AbortController()
  request = current
  loading.value = true
  error.value = null
  try {
    const result = await customersClient.search(
      session.unitId,
      { search: search.value, page: page.value, pageSize: 20 },
      current.signal
    )
    if (!current.signal.aborted) {
      rows.value = result.items
      total.value = result.total
    }
  } catch (failure) {
    if (!isCancel(failure)) {
      error.value = failure
      rows.value = []
    }
  } finally {
    if (request === current) loading.value = false
  }
}
function filter() {
  page.value = 1
  void load()
}
function open(row?: Customer, showAddresses = false) {
  customer.value = row
    ? { ...row, phone: canonicalPhone(row.phone), addresses: row.addresses.map((item) => ({ ...item })) }
    : { id: '', name: '', phone: '', email: null, addresses: [] }
  editError.value = null
  message.value = ''
  editor.value = !showAddresses
  addresses.value = showAddresses
}
function openAddress(row?: Address) {
  address.value = row ? { ...row } : blankAddress()
  editError.value = null
  addressEditor.value = true
}
function pastePhone(event: ClipboardEvent) {
  const text = event.clipboardData?.getData('text') ?? ''
  if (!/\d/.test(text)) return
  event.preventDefault()
  customer.value.phone = limitPhone(text)
}
function askRemove(item: Address) {
  address.value = { ...item }
  confirmation.value = 'remove'
}
async function save() {
  if (busy.value || !confirmation.value) return
  const action = confirmation.value
  busy.value = true
  editError.value = null
  try {
    if (action === 'customer') {
      await customersClient.save(session.unitId, customer.value.id || null, {
        name: customer.value.name,
        phone: canonicalPhone(customer.value.phone),
        email: customer.value.email || null
      })
      editor.value = false
    } else {
      if (action === 'remove')
        await customersClient.removeAddress(
          session.unitId,
          customer.value.id,
          address.value.id
        )
      else {
        const { id, ...payload } = address.value
        await customersClient.address(
          session.unitId,
          customer.value.id,
          id || null,
          { ...payload, complement: payload.complement || null }
        )
      }
      // A API define o principal de forma atômica. Recarregar evita manter duas marcas na tela.
      addressEditor.value = false
      addresses.value = false
    }
    message.value = 'Dados do cliente atualizados.'
    await load()
  } catch (failure) {
    editError.value = failure
  } finally {
    busy.value = false
    confirmation.value = null
  }
}
onMounted(load)
onUnmounted(() => request?.abort())
</script>
<template>
  <q-page class="admin-page">
    <header class="admin-page-header">
      <div>
        <p class="admin-page-eyebrow">RELACIONAMENTO</p>
        <h1 class="text-h4 q-my-sm">Clientes</h1>
        <p>Contatos e endereços da unidade selecionada.</p>
      </div>
      <q-btn
        color="primary"
        icon="person_add"
        label="Cadastrar cliente"
        :disable="!session.unitId"
        @click="open()"
      />
    </header>
    <q-banner v-if="!session.unitId" class="bg-amber-1"
      >Selecione uma unidade autorizada.</q-banner
    >
    <p v-if="message" role="status" class="text-positive">{{ message }}</p>
    <ProblemBanner :error="error"
      ><q-btn flat label="Tentar novamente" @click="load"
    /></ProblemBanner>
    <q-form class="row q-col-gutter-md q-my-md" @submit="filter"
      ><q-input
        v-model="search"
        outlined
        label="Pesquisar nome, telefone ou e-mail"
        maxlength="150"
        class="col-12 col-md-7" />
      <div class="col-12 col-md-5 q-gutter-sm">
        <q-btn type="submit" flat label="Pesquisar" :loading="loading" />
      </div
    ></q-form>
    <q-markup-table flat bordered wrap-cells :aria-busy="loading"
      ><thead>
        <tr>
          <th class="text-left">Nome</th>
          <th class="text-left">Contato</th>
          <th>Ações</th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="row in rows" :key="row.id">
          <td>{{ row.name }}</td>
          <td>{{ row.phone }}<br />{{ row.email }}</td>
          <td class="text-center">
            <q-btn flat
              square
              class="collection-action-btn"
              color="primary"
              icon="edit"
              :aria-label="`Editar ${row.name}`"
              :disable="loading"
              @click="open(row)"
            ><q-tooltip>Editar {{ row.name }}</q-tooltip></q-btn><q-btn flat
              square
              class="collection-action-btn"
              color="secondary"
              icon="location_on"
              :aria-label="`Endereços de ${row.name}`"
              :disable="loading"
              @click="open(row, true)"
            ><q-tooltip>Endereços de {{ row.name }}</q-tooltip></q-btn>
          </td>
        </tr></tbody
    ></q-markup-table>
    <p v-if="loading" role="status">Carregando clientes…</p>
    <p v-else-if="!rows.length && !error" role="status">
      Nenhum cliente encontrado.
    </p>
    <div class="row items-center justify-between q-mt-md">
      <span>{{ total }} clientes</span
      ><q-pagination
        v-model="page"
        :max="maxPage"
        :max-pages="5"
        :disable="loading"
        @update:model-value="load"
      />
    </div>
    <q-dialog v-model="editor" :persistent="busy" aria-label="Editar cliente"
      ><q-card style="width: 600px; max-width: 95vw"
        ><q-card-section
          ><h2 class="text-h5">
            {{ customer.id ? 'Editar cliente' : 'Cadastrar cliente' }}
          </h2>
          <ProblemBanner :error="editError" />
          <q-form class="q-gutter-md" @submit="confirmation = 'customer'">
            <q-input
              v-model="customer.name"
              outlined
              autofocus
              label="Nome"
              maxlength="150"
              :rules="[required]"
              :error="!!field('name')"
              :error-message="field('name')"
            />
            <q-input
              v-model="customer.phone"
              outlined
              type="tel"
              label="Telefone"
              maxlength="30"
              :mask="phoneMask(customer.phone)"
              unmasked-value
              :rules="[validPhone]"
              :error="!!field('phone')"
              :error-message="field('phone')"
              @paste="pastePhone"
            />
            <q-input
              v-model="customer.email"
              outlined
              type="email"
              label="E-mail (opcional)"
              maxlength="254"
              :error="!!field('email')"
              :error-message="field('email')"
            />
            <p v-if="!customer.id" class="text-caption">
              Se o telefone já estiver cadastrado nesta unidade, o contato
              existente será atualizado.
            </p>
            <div class="row justify-end q-gutter-sm">
              <q-btn
                flat
                label="Cancelar"
                :disable="busy"
                @click="editor = false"
              /><q-btn
                type="submit"
                color="primary"
                icon="save"
                label="Salvar"
                :loading="busy"
              />
            </div>
          </q-form> </q-card-section></q-card
    ></q-dialog>
    <q-dialog
      v-model="addresses"
      :persistent="busy"
      aria-label="Endereços do cliente"
      ><q-card style="width: 720px; max-width: 95vw"
        ><q-card-section
          ><h2 class="text-h5">Endereços — {{ customer.name }}</h2>
          <ProblemBanner :error="editError" />
          <p v-if="!customer.addresses.length">Nenhum endereço cadastrado.</p>
          <div
            v-for="item in customer.addresses"
            :key="item.id"
            class="q-mb-md q-pa-md bordered"
          >
            <h3 class="text-subtitle1">
              {{ item.label }} {{ item.isPrimary ? '— Principal' : '' }}
            </h3>
            <p>
              {{ item.street }}, {{ item.number }} {{ item.complement }}<br />{{
                item.neighborhood
              }}
              — {{ item.city }}/{{ item.state }} — {{ item.postalCode }}
            </p>
            <q-btn flat
              square
              class="collection-action-btn"
              color="primary"
              icon="edit_location_alt"
              :aria-label="`Editar endereço ${item.label}`"
              @click="openAddress(item)"
            ><q-tooltip>Editar endereço {{ item.label }}</q-tooltip></q-btn><q-btn flat
              square
              class="collection-action-btn"
              color="negative"
              icon="delete"
              :aria-label="`Remover endereço ${item.label}`"
              @click="askRemove(item)"
            ><q-tooltip>Remover endereço {{ item.label }}</q-tooltip></q-btn>
          </div>
          <div class="row justify-end q-gutter-sm">
            <q-btn
              flat
              label="Fechar"
              :disable="busy"
              @click="addresses = false"
            /><q-btn
              color="primary"
              icon="add"
              label="Adicionar endereço"
              @click="openAddress()"
            />
          </div> </q-card-section></q-card
    ></q-dialog>
    <q-dialog
      v-model="addressEditor"
      :persistent="busy"
      aria-label="Editar endereço"
      ><q-card style="width: 640px; max-width: 95vw"
        ><q-card-section
          ><h2 class="text-h5">
            {{ address.id ? 'Editar endereço' : 'Adicionar endereço' }}
          </h2>
          <ProblemBanner :error="editError" />
          <q-form class="q-gutter-md" @submit="confirmation = 'address'"
            ><q-input
              v-for="item in fields"
              :key="item.key"
              v-model="address[item.key]"
              outlined
              :label="item.label"
              :maxlength="item.max"
              :rules="item.key === 'complement' ? [] : [required]"
              :error="!!field(item.key)"
              :error-message="field(item.key)" /><q-checkbox
              v-model="address.isPrimary"
              label="Endereço principal" />
            <p class="text-caption">
              Marcar como principal substitui o endereço principal anterior.
            </p>
            <div class="row justify-end q-gutter-sm">
              <q-btn
                flat
                label="Cancelar"
                :disable="busy"
                @click="addressEditor = false"
              /><q-btn
                type="submit"
                color="primary"
                icon="save"
                label="Salvar endereço"
                :loading="busy"
              /></div
          ></q-form> </q-card-section></q-card
    ></q-dialog>
    <q-dialog
      :model-value="!!confirmation"
      persistent
      aria-label="Confirmar alteração do cliente"
      ><q-card
        ><q-card-section
          ><h2 class="text-h6">Confirmar alteração</h2>
          <p>
            {{
              confirmation === 'remove'
                ? `Remover o endereço ${address.label}?`
                : 'Salvar as alterações informadas?'
            }}
          </p></q-card-section
        ><q-card-actions align="right"
          ><q-btn
            flat
            label="Voltar"
            :disable="busy"
            @click="confirmation = null" /><q-btn
            :color="confirmation === 'remove' ? 'negative' : 'primary'"
            :icon="confirmation === 'remove' ? 'delete' : 'save'"
            :label="confirmation === 'remove' ? 'Remover endereço' : 'Salvar alterações'"
            :loading="busy"
            @click="save" /></q-card-actions></q-card
    ></q-dialog>
  </q-page>
</template>
