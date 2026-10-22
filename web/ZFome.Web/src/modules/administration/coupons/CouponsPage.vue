<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref } from 'vue'
import { isCancel } from 'axios'
import { ApiError } from '../../../http/client'
import ProblemBanner from '../../../components/ProblemBanner.vue'
import { useSessionStore } from '../../session/store'
import {
  couponsClient,
  couponPayload,
  localDateTime,
  type Coupon
} from './client'
const session = useSessionStore()
const rows = ref<Coupon[]>([]),
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
const blank = (): Coupon => ({
  id: '',
  code: '',
  description: null,
  discountType: 'Percentage',
  value: 10,
  minimumOrder: 0,
  startsAt: new Date().toISOString(),
  endsAt: new Date(Date.now() + 86400000).toISOString(),
  maximumUses: null,
  usedCount: 0,
  isActive: true
})
const coupon = ref<Coupon>(blank()),
  startsAt = ref(''),
  endsAt = ref('')
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
    const result = await couponsClient.search(
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
function open(row?: Coupon) {
  coupon.value = row ? { ...row } : blank()
  startsAt.value = localDateTime(coupon.value.startsAt)
  endsAt.value = localDateTime(coupon.value.endsAt)
  editError.value = null
  message.value = ''
  editor.value = true
}
function toggle(row: Coupon) {
  coupon.value = { ...row }
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
      await couponsClient.active(
        session.unitId,
        coupon.value.id,
        !coupon.value.isActive
      )
    else
      await couponsClient.save(
        session.unitId,
        coupon.value.id || null,
        couponPayload(coupon.value, startsAt.value, endsAt.value)
      )
    editor.value = false
    confirmation.value = null
    message.value = 'Cupom atualizado.'
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
        <p class="admin-page-eyebrow">PROMOÇÕES</p>
        <h1 class="text-h4 q-my-sm">Cupons</h1>
        <p>Regras, validade e disponibilidade das promoções da unidade.</p>
      </div>
      <q-btn color="primary" icon="add" label="Cadastrar cupom" :disable="!session.unitId" @click="open()" />
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
        label="Pesquisar cupom"
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
          <th class="text-left">Código</th>
          <th>Desconto</th>
          <th>Usos</th>
          <th>Estado</th>
          <th>Ações</th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="row in rows" :key="row.id">
          <td>{{ row.code }}</td>
          <td class="text-center">
            {{
              row.discountType === 'Percentage'
                ? `${row.value}%`
                : row.value.toLocaleString('pt-BR', {
                    style: 'currency',
                    currency: 'BRL'
                  })
            }}
          </td>
          <td class="text-center">
            {{ row.usedCount }} / {{ row.maximumUses ?? 'Sem limite' }}
          </td>
          <td class="text-center">{{ row.isActive ? 'Ativo' : 'Inativo' }}</td>
          <td class="text-center">
            <q-btn flat
              square
              class="collection-action-btn"
              color="primary"
              icon="edit"
              :aria-label="`Editar cupom ${row.code}`"
              :disable="loading"
              @click="open(row)"
            ><q-tooltip>Editar cupom {{ row.code }}</q-tooltip></q-btn><q-btn flat
              square
              class="collection-action-btn"
              :color="row.isActive ? 'negative' : 'positive'"
              :icon="row.isActive ? 'toggle_off' : 'toggle_on'"
              :aria-label="`${row.isActive ? 'Desativar' : 'Ativar'} cupom ${row.code}`"
              :disable="loading"
              @click="toggle(row)"
            ><q-tooltip>{{ row.isActive ? 'Desativar' : 'Ativar' }} cupom {{ row.code }}</q-tooltip></q-btn>
          </td>
        </tr></tbody
    ></q-markup-table>
    <p v-if="loading" role="status">Carregando cupons…</p>
    <p v-else-if="!rows.length && !error" role="status">
      Nenhum cupom encontrado.
    </p>
    <div class="row items-center justify-between q-mt-md">
      <span>{{ total }} cupons</span
      ><q-pagination
        v-model="page"
        :max="maxPage"
        :max-pages="5"
        :disable="loading"
        @update:model-value="load"
      />
    </div>
    <q-dialog v-model="editor" :persistent="busy" aria-label="Editar cupom"
      ><q-card style="width: 640px; max-width: 95vw"
        ><q-card-section
          ><h2 class="text-h5">
            {{ coupon.id ? 'Editar cupom' : 'Cadastrar cupom' }}
          </h2>
          <ProblemBanner :error="editError" />
          <q-form class="q-gutter-md" @submit="confirmation = 'save'">
            <q-input
              v-model="coupon.code"
              outlined
              autofocus
              label="Código"
              maxlength="40"
              :rules="[required]"
              :error="!!field('code')"
              :error-message="field('code')"
            />
            <q-input
              v-model="coupon.description"
              outlined
              label="Descrição"
              type="textarea"
              maxlength="300"
              :error="!!field('description')"
              :error-message="field('description')"
            />
            <q-select
              v-model="coupon.discountType"
              outlined
              label="Tipo de desconto"
              emit-value
              map-options
              :options="[
                { value: 'Percentage', label: 'Percentual' },
                { value: 'FixedAmount', label: 'Valor fixo' }
              ]"
            />
            <q-input
              v-model.number="coupon.value"
              outlined
              type="number"
              :label="
                coupon.discountType === 'Percentage'
                  ? 'Desconto (%)'
                  : 'Desconto (R$)'
              "
              min="0.01"
              :max="coupon.discountType === 'Percentage' ? 100 : undefined"
              step="0.01"
              :rules="[(value) => value > 0 || 'Informe um valor positivo']"
              :error="!!field('value')"
              :error-message="field('value')"
            />
            <q-input
              v-model.number="coupon.minimumOrder"
              outlined
              type="number"
              label="Pedido mínimo (R$)"
              min="0"
              step="0.01"
              :rules="[
                (value) =>
                  (value !== '' && value >= 0) ||
                  'Informe um valor não negativo'
              ]"
              :error="!!field('minimumOrder')"
              :error-message="field('minimumOrder')"
            />
            <p class="text-caption">
              Datas no fuso deste dispositivo:
              {{ Intl.DateTimeFormat().resolvedOptions().timeZone }}.
            </p>
            <q-input
              v-model="startsAt"
              outlined
              type="datetime-local"
              step="1"
              label="Início da validade"
              :rules="[required]"
              :error="!!field('startsAt')"
              :error-message="field('startsAt')"
            />
            <q-input
              v-model="endsAt"
              outlined
              type="datetime-local"
              step="1"
              label="Fim da validade"
              :rules="[
                required,
                (value) =>
                  new Date(value) > new Date(startsAt) ||
                  'Fim deve ser posterior ao início'
              ]"
              :error="!!field('endsAt')"
              :error-message="field('endsAt')"
            />
            <q-input
              v-model.number="coupon.maximumUses"
              outlined
              clearable
              type="number"
              label="Limite de usos (opcional)"
              min="1"
              step="1"
              :error="!!field('maximumUses')"
              :error-message="field('maximumUses')"
            />
            <p v-if="coupon.id">
              Usos já registrados: {{ coupon.usedCount }}. O histórico não é
              alterado.
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
      aria-label="Confirmar alteração do cupom"
      ><q-card
        ><q-card-section
          ><h2 class="text-h6">Confirmar alteração</h2>
          <p>
            {{
              confirmation === 'active'
                ? `${coupon.isActive ? 'Desativar' : 'Ativar'} o cupom ${coupon.code}?`
                : 'Salvar as regras e a validade informadas?'
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
            :color="confirmation === 'active' && coupon.isActive ? 'negative' : confirmation === 'active' ? 'positive' : 'primary'"
            :icon="confirmation === 'active' ? (coupon.isActive ? 'toggle_off' : 'toggle_on') : 'save'"
            :label="confirmation === 'active' ? (coupon.isActive ? 'Desativar cupom' : 'Ativar cupom') : 'Salvar alterações'"
            :loading="busy"
            @click="save" /></q-card-actions></q-card
    ></q-dialog>
  </q-page>
</template>
