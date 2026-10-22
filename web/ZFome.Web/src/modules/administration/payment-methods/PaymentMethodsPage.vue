<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref } from 'vue'
import { isCancel } from 'axios'
import { ApiError } from '../../../http/client'
import ProblemBanner from '../../../components/ProblemBanner.vue'
import { useSessionStore } from '../../session/store'
import { paymentMethodsClient, type PaymentMethod } from './client'
const session = useSessionStore()
const rows = ref<PaymentMethod[]>([]),
  search = ref(''),
  state = ref('all'),
  page = ref(1),
  total = ref(0)
const loading = ref(false),
  busy = ref(false),
  error = ref<unknown>(null),
  editError = ref<unknown>(null),
  message = ref('')
const editor = ref(false),
  confirmation = ref<'save' | 'active' | null>(null)
const method = ref<PaymentMethod>({
  id: '',
  code: '',
  name: '',
  isOnline: false,
  allowsChange: false,
  isActive: true
})
const maxPage = computed(() => Math.max(1, Math.ceil(total.value / 20)))
const required = (value: string) => !!value?.trim() || 'Campo obrigatório'
const field = (key: string) =>
  editError.value instanceof ApiError ? editError.value.field(key) : undefined
let request: AbortController | undefined
async function load() {
  if (!session.unitId) return
  request?.abort()
  const current = new AbortController()
  request = current
  loading.value = true
  error.value = null
  try {
    const result = await paymentMethodsClient.search(
      session.unitId,
      {
        search: search.value,
        isActive: state.value === 'all' ? undefined : state.value === 'active',
        page: page.value,
        pageSize: 20
      },
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
function open(row?: PaymentMethod) {
  method.value = row
    ? { ...row }
    : {
        id: '',
        code: '',
        name: '',
        isOnline: false,
        allowsChange: false,
        isActive: true
      }
  editError.value = null
  message.value = ''
  editor.value = true
}
function toggle(row: PaymentMethod) {
  method.value = { ...row }
  editError.value = null
  confirmation.value = 'active'
}
async function save() {
  if (busy.value || !confirmation.value) return
  busy.value = true
  editError.value = null
  const action = confirmation.value
  try {
    if (action === 'active')
      await paymentMethodsClient.active(
        session.unitId,
        method.value.id,
        !method.value.isActive
      )
    else {
      const { code, name, isOnline, allowsChange } = method.value
      await paymentMethodsClient.save(session.unitId, method.value.id || null, {
        code,
        name,
        isOnline,
        allowsChange
      })
    }
    editor.value = false
    confirmation.value = null
    message.value = 'Forma de pagamento atualizada.'
    await load()
  } catch (failure) {
    editError.value = failure
    if (action === 'save') confirmation.value = null
  } finally {
    busy.value = false
  }
}
onMounted(load)
onUnmounted(() => request?.abort())
</script>
<template>
  <q-page class="admin-page">
    <header class="admin-page-header">
      <div>
        <p class="admin-page-eyebrow">FINANCEIRO</p>
        <h1 class="text-h4 q-my-sm">Formas de pagamento</h1>
        <p>Configure as opções disponíveis para novas cobranças.</p>
      </div>
      <q-btn color="primary" icon="add" label="Cadastrar forma" :disable="!session.unitId" @click="open()" />
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
        label="Pesquisar código ou nome"
        maxlength="100"
        class="col-12 col-md-5" /><q-select
        v-model="state"
        outlined
        label="Estado"
        emit-value
        map-options
        :options="[
          { value: 'all', label: 'Todos' },
          { value: 'active', label: 'Ativos' },
          { value: 'inactive', label: 'Inativos' }
        ]"
        class="col-12 col-md-3" />
      <div class="col-12 col-md-4 q-gutter-sm">
        <q-btn type="submit" flat label="Pesquisar" :loading="loading" />
      </div
    ></q-form>
    <q-markup-table flat bordered wrap-cells :aria-busy="loading"
      ><thead>
        <tr>
          <th class="text-left">Código / Nome</th>
          <th>Online</th>
          <th>Troco</th>
          <th>Estado</th>
          <th>Ações</th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="row in rows" :key="row.id">
          <td>{{ row.code }} — {{ row.name }}</td>
          <td class="text-center">{{ row.isOnline ? 'Sim' : 'Não' }}</td>
          <td class="text-center">{{ row.allowsChange ? 'Sim' : 'Não' }}</td>
          <td class="text-center">{{ row.isActive ? 'Ativa' : 'Inativa' }}</td>
          <td class="text-center">
            <q-btn flat
              square
              class="collection-action-btn"
              color="primary"
              icon="edit"
              :aria-label="`Editar forma ${row.name}`"
              :disable="loading"
              @click="open(row)"
            ><q-tooltip>Editar {{ row.name }}</q-tooltip></q-btn><q-btn flat
              square
              class="collection-action-btn"
              :color="row.isActive ? 'negative' : 'positive'"
              :icon="row.isActive ? 'toggle_off' : 'toggle_on'"
              :aria-label="`${row.isActive ? 'Desativar' : 'Ativar'} forma ${row.name}`"
              :disable="loading"
              @click="toggle(row)"
            ><q-tooltip>{{ row.isActive ? 'Desativar' : 'Ativar' }} forma {{ row.name }}</q-tooltip></q-btn>
          </td>
        </tr></tbody
    ></q-markup-table>
    <p v-if="loading" role="status">Carregando formas de pagamento…</p>
    <p v-else-if="!rows.length && !error" role="status">
      Nenhuma forma de pagamento encontrada.
    </p>
    <div class="row items-center justify-between q-mt-md">
      <span>{{ total }} formas</span
      ><q-pagination
        v-model="page"
        :max="maxPage"
        :max-pages="5"
        :disable="loading"
        @update:model-value="load"
      />
    </div>
    <q-dialog
      v-model="editor"
      :persistent="busy"
      aria-label="Editar forma de pagamento"
      ><q-card style="width: 600px; max-width: 95vw"
        ><q-card-section
          ><h2 class="text-h5">
            {{
              method.id
                ? 'Editar forma de pagamento'
                : 'Cadastrar forma de pagamento'
            }}
          </h2>
          <ProblemBanner :error="editError" />
          <q-form class="q-gutter-md" @submit="confirmation = 'save'">
            <q-input
              v-model="method.code"
              outlined
              autofocus
              label="Código"
              maxlength="30"
              :rules="[required]"
              :error="!!field('code')"
              :error-message="field('code')"
            />
            <q-input
              v-model="method.name"
              outlined
              label="Nome"
              maxlength="100"
              :rules="[required]"
              :error="!!field('name')"
              :error-message="field('name')"
            />
            <q-checkbox
              v-model="method.isOnline"
              label="Pagamento online"
            /><q-checkbox v-model="method.allowsChange" label="Permite troco" />
            <p class="text-caption">
              Esta configuração não cria integração com um provedor de
              pagamento.
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
      :model-value="!!confirmation"
      persistent
      aria-label="Confirmar alteração da forma de pagamento"
      ><q-card
        ><q-card-section
          ><h2 class="text-h6">Confirmar alteração</h2>
          <p>
            {{
              confirmation === 'active'
                ? `${method.isActive ? 'Desativar' : 'Ativar'} a forma ${method.name}? Os pagamentos históricos serão preservados.`
                : 'Salvar os dados da forma de pagamento?'
            }}
          </p>
          <ProblemBanner
            v-if="confirmation === 'active'"
            :error="editError" /></q-card-section
        ><q-card-actions align="right"
          ><q-btn
            flat
            label="Voltar"
            :disable="busy"
            @click="confirmation = null" /><q-btn
            :color="confirmation === 'active' && method.isActive ? 'negative' : confirmation === 'active' ? 'positive' : 'primary'"
            :icon="confirmation === 'active' ? (method.isActive ? 'toggle_off' : 'toggle_on') : 'save'"
            :label="confirmation === 'active' ? (method.isActive ? 'Desativar forma' : 'Ativar forma') : 'Salvar alterações'"
            :loading="busy"
            @click="save" /></q-card-actions></q-card
    ></q-dialog>
  </q-page>
</template>
