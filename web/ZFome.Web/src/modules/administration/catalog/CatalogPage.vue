<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref } from 'vue'
import { isCancel } from 'axios'
import { ApiError } from '../../../http/client'
import ProblemBanner from '../../../components/ProblemBanner.vue'
import { useSessionStore } from '../../session/store'
import CatalogPicker from './CatalogPicker.vue'
import CatalogCompactTable from './CatalogCompactTable.vue'
import CatalogProductVisualGrid from './CatalogProductVisualGrid.vue'
import {
  catalogClient,
  groupPayload,
  productPayload,
  type Catalog,
  type Category,
  type Product,
  type Additional,
  type Group,
  type Resource,
  type ReusableItem,
  type ReusableResource
} from './client'
const session = useSessionStore()
const kind = ref<Resource>('categories'),
  view = ref<'compact' | 'visual'>('compact'),
  search = ref(''),
  state = ref('all'),
  page = ref(1),
  total = ref(0)
const catalog = ref<Catalog | null>(null),
  rows = ref<(Category | Product | ReusableItem)[]>([])
const loading = ref(false),
  busy = ref(false),
  editor = ref(false),
  confirmation = ref(false)
const error = ref<unknown>(null),
  editError = ref<unknown>(null),
  message = ref('')
const id = ref<string | null>(null),
  categoryId = ref('')
const picker = ref<ReusableResource | null>(null)
const category = ref<Category>({
  id: '',
  parentId: null,
  name: '',
  description: null,
  order: 0,
  imageUrl: null,
  isActive: true,
  products: []
})
const product = ref<Product>({
  id: '',
  code: '',
  name: '',
  description: null,
  basePrice: 0,
  isFeatured: false,
  isActive: true,
  allowsNotes: true,
  images: [],
  variations: [],
  additionalGroups: []
})
const additional = ref<Additional>({
  id: '',
  name: '',
  price: 0,
  isActive: true,
  order: 0
})
const group = ref<Group>({
  id: '',
  name: '',
  minimumSelection: 0,
  maximumSelection: 1,
  isActive: true,
  order: 0,
  items: [],
  pricingStrategy: 'Additive',
  type: 'Additional',
  requiresCompleteComposition: false
})
const labels: Record<Resource, string> = {
  categories: 'Categorias',
  additionals: 'Adicionais',
  'additional-groups': 'Grupos de adicionais',
  products: 'Produtos'
}
const maxPage = computed(() => Math.max(1, Math.ceil(total.value / 20)))
const products = computed(() => rows.value as Product[])
const categoryName = (productId: string) =>
  catalog.value?.categories.find((item) =>
    item.products.some((product) => product.id === productId)
  )?.name ?? 'Sem categoria'
const modifierTargets = computed(() => (catalog.value?.categories ?? []).flatMap(category => category.products.flatMap(product => product.additionalGroups.flatMap(targetGroup => targetGroup.items.map(target => ({ groupId: targetGroup.id, groupName: targetGroup.name, optionId: target.id, optionName: target.name }))))))
const targetOptions = (groupId: string) => modifierTargets.value.filter(target => target.groupId === groupId).map(target => ({ label: `${target.groupName} — ${target.optionName}`, value: target.optionId }))
const draft = computed(() =>
  kind.value === 'categories'
    ? category.value
    : kind.value === 'products'
      ? product.value
      : kind.value === 'additionals'
        ? additional.value
        : group.value
)
const required = (value: string) => !!value?.trim() || 'Campo obrigatório'
const nonnegative = (value: number) =>
  (value !== null && Number.isFinite(Number(value)) && Number(value) >= 0) ||
  'Informe um número não negativo'
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
    const tree = await catalogClient.get(session.unitId, current.signal)
    if (current.signal.aborted) return
    catalog.value = tree
    if (kind.value === 'additionals' || kind.value === 'additional-groups') {
      const result = await catalogClient.search(
        session.unitId,
        kind.value,
        {
          search: search.value,
          isActive:
            state.value === 'all' ? undefined : state.value === 'active',
          page: page.value,
          pageSize: 20
        },
        current.signal
      )
      if (!current.signal.aborted) {
        rows.value = result.items
        total.value = result.total
      }
    } else {
      const all =
        kind.value === 'categories'
          ? tree.categories
          : tree.categories.flatMap((item) => item.products)
      const filtered = all.filter(
        (item) =>
          item.name
            .toLocaleLowerCase()
            .includes(search.value.toLocaleLowerCase()) &&
          (state.value === 'all' ||
            item.isActive === (state.value === 'active'))
      )
      total.value = filtered.length
      rows.value = filtered.slice((page.value - 1) * 20, page.value * 20)
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
function open(row?: Category | Product | ReusableItem) {
  id.value = row?.id ?? null
  editError.value = null
  message.value = ''
  const copy = row ? JSON.parse(JSON.stringify(row)) : null
  if (kind.value === 'categories')
    category.value = copy ?? {
      id: '',
      name: '',
      description: null,
      parentId: null,
      order: 0,
      imageUrl: null,
      isActive: true,
      products: []
    }
  if (kind.value === 'products') {
    product.value = copy ?? {
      id: '',
      code: '',
      name: '',
      description: null,
      basePrice: 0,
      isFeatured: false,
      isActive: true,
      allowsNotes: true,
      images: [],
      variations: [],
      additionalGroups: []
    }
    categoryId.value =
      catalog.value?.categories.find((item) =>
        item.products.some((p) => p.id === row?.id)
      )?.id ??
      catalog.value?.categories[0]?.id ??
      ''
  }
  if (kind.value === 'additionals')
    additional.value = copy ?? {
      id: '',
      name: '',
      price: 0,
      isActive: true,
      order: 0
    }
  if (kind.value === 'additional-groups')
    group.value = copy ?? {
      id: '',
      name: '',
      minimumSelection: 0,
      maximumSelection: 1,
      isActive: true,
      order: 0,
      items: []
    }
  editor.value = true
}
async function save() {
  if (busy.value) return
  busy.value = true
  editError.value = null
  try {
    const payload =
      kind.value === 'products'
        ? productPayload(product.value, categoryId.value)
        : kind.value === 'additional-groups'
          ? groupPayload(group.value)
          : kind.value === 'categories'
            ? {
                name: category.value.name,
                description: category.value.description || null,
                parentId: category.value.parentId || null,
                order: category.value.order,
                imageUrl: category.value.imageUrl || null,
                isActive: category.value.isActive
              }
            : {
                name: additional.value.name,
                price: additional.value.price,
                isActive: additional.value.isActive
              }
    await catalogClient.save(session.unitId, kind.value, id.value, payload)
    editor.value = false
    message.value = 'Catálogo atualizado.'
    await load()
  } catch (failure) {
    editError.value = failure
  } finally {
    busy.value = false
    confirmation.value = false
  }
}
function select(item: ReusableItem) {
  if (
    picker.value === 'additionals' &&
    !group.value.items.some((x) => x.id === item.id)
  )
    group.value.items.push({
      ...(item as Additional),
      order: group.value.items.length
    })
  if (
    picker.value === 'additional-groups' &&
    !product.value.additionalGroups.some((x) => x.id === item.id)
  )
    product.value.additionalGroups.push({
      ...(item as Group),
      order: product.value.additionalGroups.length
    })
  picker.value = null
}
onMounted(load)
onUnmounted(() => request?.abort())
</script>
<template>
  <q-page class="admin-page">
    <header class="admin-page-header">
      <div>
        <p class="admin-page-eyebrow">GESTÃO</p>
        <h1 class="text-h4 q-my-sm">Catálogo</h1>
        <p>Organize os produtos e complementos da unidade selecionada.</p>
      </div>
      <q-btn
        color="primary"
        icon="add"
        label="Cadastrar"
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
    <q-tabs
      v-model="kind"
      align="left"
      active-color="primary"
      @update:model-value="filter"
      ><q-tab
        v-for="(label, key) in labels"
        :key="key"
        :name="key"
        :label="label"
    /></q-tabs>
    <q-form class="row q-col-gutter-md q-my-md" @submit="filter"
      ><q-input
        v-model="search"
        outlined
        label="Pesquisar nome"
        maxlength="150"
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
    <div v-if="kind === 'products'" class="admin-view-switcher" aria-label="Visualização dos produtos">
      <button type="button" :aria-pressed="view === 'visual'" @click="view = 'visual'">Visual</button>
      <button type="button" :aria-pressed="view === 'compact'" @click="view = 'compact'">Compacta</button>
    </div>
    <CatalogProductVisualGrid
      v-if="kind === 'products' && view === 'visual'"
      :products="products"
      :loading="loading"
      :category-name="categoryName"
      @edit="open"
    />
    <CatalogCompactTable v-else :rows="rows" :loading="loading" @edit="open" />
    <p v-if="loading" role="status">Carregando catálogo…</p>
    <p v-else-if="!rows.length && !error" role="status">
      Nenhum resultado para os filtros informados.
    </p>
    <div class="row items-center justify-between q-mt-md">
      <span>{{ total }} registros</span
      ><q-pagination
        v-model="page"
        :max="maxPage"
        :max-pages="5"
        :disable="loading"
        @update:model-value="load"
      />
    </div>
    <q-dialog v-model="editor" :persistent="busy" aria-label="Editar catálogo"
      ><q-card style="width: 840px; max-width: 95vw"
        ><q-card-section>
          <h2 class="text-h5">
            {{ id ? 'Editar' : 'Cadastrar' }} — {{ labels[kind] }}
          </h2>
          <ProblemBanner :error="editError" />
          <q-form class="q-gutter-md" @submit="confirmation = true">
            <q-input
              v-model="draft.name"
              outlined
              label="Nome"
              maxlength="150"
              :rules="[required]"
              :disable="busy"
              :error="!!field('name')"
              :error-message="field('name')"
            />
            <q-checkbox
              v-model="draft.isActive"
              label="Ativo"
              :disable="busy"
            />
            <template v-if="kind === 'categories'">
              <q-input
                v-model="category.description"
                outlined
                type="textarea"
                label="Descrição"
                maxlength="500"
              />
              <q-input
                v-model.number="category.order"
                outlined
                type="number"
                label="Ordem"
                :rules="[nonnegative]"
                min="0"
              />
              <q-input
                v-model="category.imageUrl"
                outlined
                label="URL da imagem"
                maxlength="500"
                :error="!!field('imageUrl')"
                :error-message="field('imageUrl')"
              />
              <q-select
                v-model="category.parentId"
                outlined
                clearable
                label="Categoria pai (opcional)"
                :options="
                  catalog?.categories.filter((item) => item.id !== id) ?? []
                "
                option-label="name"
                option-value="id"
                emit-value
                map-options
              />
            </template>
            <template v-if="kind === 'additionals'"
              ><q-input
                v-model.number="additional.price"
                outlined
                type="number"
                label="Preço"
                min="0"
                step="0.01"
                :rules="[nonnegative]"
                :error="!!field('price')"
                :error-message="field('price')"
            /></template>
            <template v-if="kind === 'additional-groups'">
              <q-select v-model="group.type" outlined label="Tipo do grupo" emit-value map-options
                :options="[{label:'Adicionais',value:'Additional'},{label:'Sabores',value:'Flavor'},{label:'Bordas',value:'Crust'},{label:'Remoções',value:'Removal'}]" />
              <q-select v-model="group.pricingStrategy" outlined label="Estratégia de preço" emit-value map-options
                :options="[{label:'Somar opções',value:'Additive'},{label:'Maior preço',value:'HighestPrice'},{label:'Proporcional à fração',value:'Proportional'},{label:'Sem alteração',value:'NoPriceChange'}]" />
              <q-checkbox v-if="group.pricingStrategy === 'HighestPrice' || group.pricingStrategy === 'Proportional'"
                v-model="group.requiresCompleteComposition" label="Exigir composição integral (frações totalizam 1)" />
              <q-input
                v-model.number="group.minimumSelection"
                outlined
                type="number"
                label="Seleção mínima"
                min="0"
                :rules="[nonnegative]"
              />
              <q-input
                v-model.number="group.maximumSelection"
                outlined
                type="number"
                label="Seleção máxima"
                min="1"
                :rules="[
                  (value) =>
                    value >= Math.max(1, group.minimumSelection) ||
                    'Máximo deve respeitar o mínimo'
                ]"
              />
              <h3 class="text-subtitle1">Adicionais do grupo</h3>
              <p class="text-caption">
                Itens inativos já associados são preservados até você
                removê-los.
              </p>
              <div
                v-for="(item, index) in group.items"
                :key="item.id"
                class="row items-center q-gutter-sm"
              >
                <span class="col"
                  >{{ item.name }} {{ item.isActive ? '' : '(inativo)' }}</span
                ><q-input
                  v-model.number="item.order"
                  outlined
                  dense
                  type="number"
                  label="Ordem"
                  style="width: 100px"
                  min="0"
                  :rules="[nonnegative]"
                /><q-btn flat
                  square
                  class="collection-action-btn"
                  color="negative"
                  icon="delete"
                  :aria-label="'Remover vínculo com ' + item.name"
                  @click="group.items.splice(index, 1)"
                ><q-tooltip>Remover vínculo</q-tooltip></q-btn>
                <div v-for="(rule, ruleIndex) in (item.compatibilityRules ??= [])" :key="ruleIndex" class="row col-12 items-center q-gutter-sm">
                  <q-select v-model="rule.kind" outlined dense label="Regra" emit-value map-options
                    :options="[{label:'Exige',value:'Requires'},{label:'Exclui',value:'Excludes'}]" style="min-width: 150px" />
                  <q-select v-model="rule.targetGroupId" outlined dense label="Grupo alvo" emit-value map-options
                    :options="Array.from(new Map(modifierTargets.map(target => [target.groupId, {label:target.groupName,value:target.groupId}])).values())" style="min-width: 180px" />
                  <q-select v-model="rule.targetAdditionalId" outlined dense label="Opção alvo" emit-value map-options
                    :options="targetOptions(rule.targetGroupId)" style="min-width: 200px" />
                  <q-btn flat square class="collection-action-btn" color="negative" icon="delete" :aria-label="'Remover regra ' + (ruleIndex + 1)" @click="item.compatibilityRules?.splice(ruleIndex, 1)"><q-tooltip>Remover regra</q-tooltip></q-btn>
                </div>
                <q-btn flat icon="add" label="Adicionar dependência/exclusão" @click="(item.compatibilityRules ??= []).push({targetGroupId:'',targetAdditionalId:'',kind:'Requires'})" />
              </div>
              <q-btn
                outline
                icon="add"
                label="Adicionar vínculo"
                @click="picker = 'additionals'"
              />
            </template>
            <template v-if="kind === 'products'">
              <q-select
                v-model="categoryId"
                outlined
                label="Categoria"
                :options="catalog?.categories ?? []"
                option-label="name"
                option-value="id"
                emit-value
                map-options
                :rules="[required]"
              />
              <q-input
                v-model="product.code"
                outlined
                label="Código"
                maxlength="50"
                :rules="[required]"
                :error="!!field('code')"
                :error-message="field('code')"
              />
              <q-input
                v-model="product.description"
                outlined
                type="textarea"
                label="Descrição"
                maxlength="1000"
              />
              <q-input
                v-model.number="product.basePrice"
                outlined
                type="number"
                label="Preço base"
                min="0"
                step="0.01"
                :rules="[nonnegative]"
              />
              <q-checkbox
                v-model="product.isFeatured"
                label="Destaque"
              /><q-checkbox
                v-model="product.allowsNotes"
                label="Permite observações"
              />
              <h3 class="text-subtitle1">Imagens</h3>
              <div
                v-for="(item, index) in product.images"
                :key="index"
                class="q-gutter-sm q-pa-sm rounded-borders bg-grey-1"
              >
                <q-input
                  v-model="item.url"
                  outlined
                  label="URL da imagem"
                  :rules="[required]"
                  maxlength="500"
                /><q-input
                  v-model.number="item.order"
                  outlined
                  type="number"
                  label="Ordem da imagem"
                  min="0"
                  :rules="[nonnegative]"
                /><q-checkbox
                  v-model="item.isPrincipal"
                  label="Principal"
                /><q-btn flat
                  square
                  class="collection-action-btn"
                  color="negative"
                  icon="delete"
                  :aria-label="'Remover imagem ' + (index + 1)"
                  @click="product.images.splice(index, 1)"
                ><q-tooltip>Remover imagem</q-tooltip></q-btn>
              </div>
              <q-btn
                outline
                icon="add"
                label="Adicionar imagem"
                @click="
                  product.images.push({
                    url: '',
                    order: product.images.length,
                    isPrincipal: !product.images.length
                  })
                "
              />
              <h3 class="text-subtitle1">Variações</h3>
              <div
                v-for="(item, index) in product.variations"
                :key="index"
                class="q-gutter-sm q-pa-sm rounded-borders bg-grey-1"
              >
                <q-input
                  v-model="item.name"
                  outlined
                  label="Nome da variação"
                  maxlength="100"
                  :rules="[required]"
                /><q-input
                  v-model.number="item.price"
                  outlined
                  type="number"
                  label="Preço da variação"
                  min="0"
                  step="0.01"
                  :rules="[nonnegative]"
                /><q-input
                  v-model.number="item.order"
                  outlined
                  type="number"
                  label="Ordem da variação"
                  min="0"
                  :rules="[nonnegative]"
                /><q-checkbox
                  v-model="item.isActive"
                  label="Variação ativa"
                /><q-btn flat
                  square
                  class="collection-action-btn"
                  color="negative"
                  icon="delete"
                  :aria-label="'Remover variação ' + item.name"
                  @click="product.variations.splice(index, 1)"
                ><q-tooltip>Remover variação</q-tooltip></q-btn>
              </div>
              <q-btn
                outline
                icon="add"
                label="Adicionar variação"
                @click="
                  product.variations.push({
                    name: '',
                    price: 0,
                    order: product.variations.length,
                    isActive: true
                  })
                "
              />
              <h3 class="text-subtitle1">Grupos de adicionais</h3>
              <div
                v-for="(item, index) in product.additionalGroups"
                :key="item.id"
                class="row items-center q-gutter-sm"
              >
                <span class="col"
                  >{{ item.name }} {{ item.isActive ? '' : '(inativo)' }}</span
                ><q-input
                  v-model.number="item.order"
                  outlined
                  dense
                  type="number"
                  label="Ordem do grupo"
                  min="0"
                  :rules="[nonnegative]"
                  style="width: 110px"
                /><q-btn flat
                  square
                  class="collection-action-btn"
                  color="negative"
                  icon="delete"
                  :aria-label="'Remover grupo ' + item.name"
                  @click="product.additionalGroups.splice(index, 1)"
                ><q-tooltip>Remover grupo</q-tooltip></q-btn>
              </div>
              <q-btn
                outline
                icon="add"
                label="Vincular grupo"
                @click="picker = 'additional-groups'"
              />
            </template>
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
      :model-value="!!picker"
      @update:model-value="picker = null"
      aria-label="Selecionar vínculo do catálogo"
      ><CatalogPicker
        v-if="picker"
        :unit-id="session.unitId"
        :resource="picker"
        :excluded-ids="
          picker === 'additionals'
            ? group.items.map((x) => x.id)
            : product.additionalGroups.map((x) => x.id)
        "
        @selected="select"
        @close="picker = null"
    /></q-dialog>
    <q-dialog
      v-model="confirmation"
      persistent
      aria-label="Confirmar alterações do catálogo"
      ><q-card
        ><q-card-section
          ><h2 class="text-h6">Confirmar alterações</h2>
          <p>
            Salvar os dados e vínculos informados para {{ draft.name }}?
          </p></q-card-section
        ><q-card-actions align="right"
          ><q-btn
            flat
            label="Voltar"
            :disable="busy"
            @click="confirmation = false" /><q-btn
            color="primary"
            icon="save"
            label="Salvar alterações"
            :loading="busy"
            @click="save" /></q-card-actions></q-card
    ></q-dialog>
  </q-page>
</template>
