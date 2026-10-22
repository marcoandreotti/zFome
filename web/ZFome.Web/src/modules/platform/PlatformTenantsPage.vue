<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { useRouter } from 'vue-router'
import ProblemBanner from '../../components/ProblemBanner.vue'
import { useSessionStore } from '../session/store'
import { usePlatformStore } from './store'

const router = useRouter(); const session = useSessionStore(); const platform = usePlatformStore()
const search = ref(''); const showProvision = ref(false); const showUnit = ref(false); const selectedTenantId = ref('')
const error = ref<unknown>(null); const submitting = ref(false)
const form = reactive({ tenantName: '', tenantPublicCode: '', establishmentName: '', establishmentSlug: '', timeZoneId: 'America/Sao_Paulo', ownerName: '', ownerEmail: '', temporaryPassword: '' })
const unit = reactive({ name: '', slug: '', timeZoneId: 'America/Sao_Paulo', ownerId: '' })
const required = (value: string) => !!value?.trim() || 'Campo obrigatório'
onMounted(() => platform.load().catch(failure => { error.value = failure }))
async function reload() { error.value = null; try { await platform.load(search.value) } catch (failure) { error.value = failure } }
async function continueToUnit(result: { establishmentId: string; nextStep: string }) {
  await session.hydrate(); session.selectUnit(result.establishmentId); await router.push(result.nextStep)
}
async function submitProvision() {
  submitting.value = true; error.value = null
  try { const result = await platform.provision(form); showProvision.value = false; await continueToUnit(result) }
  catch (failure) { error.value = failure } finally { submitting.value = false }
}
async function openUnit(tenant: { id: string; establishments: { id: string }[] }) {
  error.value = null
  const contextUnit = tenant.establishments[0]
  if (!contextUnit) { error.value = new Error('O Tenant precisa possuir uma unidade para selecionar seu Owner.'); return }
  try { await platform.loadOwners(contextUnit.id); selectedTenantId.value = tenant.id; unit.ownerId = platform.owners[0]?.id ?? ''; showUnit.value = true; platform.resetIntent() }
  catch (failure) { error.value = failure }
}
async function submitUnit() {
  submitting.value = true; error.value = null
  try { const result = await platform.addUnit(selectedTenantId.value, unit); showUnit.value = false; await continueToUnit(result) }
  catch (failure) { error.value = failure } finally { submitting.value = false }
}
async function recover() {
  error.value = null
  try { const result = await platform.recover(); if (result) await continueToUnit(result) }
  catch (failure) { error.value = failure }
}
</script>
<template>
  <q-page padding class="admin-page">
    <div class="row items-start justify-between q-col-gutter-md">
      <div><div class="text-overline text-primary">PLATAFORMA</div><h1 class="text-h4 q-my-sm">Tenants e unidades</h1><p class="text-grey-8">Provisione clientes sem criar associação artificial para sua identidade global.</p></div>
      <q-btn color="primary" unelevated icon="add_business" label="Provisionar Tenant" @click="showProvision = true; platform.resetIntent()" />
    </div>
    <ProblemBanner :error="error" />
    <div class="row q-gutter-sm q-mb-lg"><q-input v-model="search" outlined dense label="Pesquisar" @keyup.enter="reload" /><q-btn outline label="Buscar" :loading="platform.busy" @click="reload" /></div>
    <q-banner v-if="!platform.busy && !platform.tenants.length" class="bg-amber-1 rounded-borders">Nenhum Tenant cadastrado. Provisione o primeiro Tenant para liberar a operação.</q-banner>
    <q-list v-else bordered separator class="rounded-borders">
      <q-expansion-item v-for="tenant in platform.tenants" :key="tenant.id" :label="tenant.name" :caption="tenant.publicCode">
        <q-card><q-card-section><q-list dense><q-item v-for="item in tenant.establishments" :key="item.id"><q-item-section><q-item-label>{{ item.name }}</q-item-label><q-item-label caption>{{ item.slug }} · {{ item.onboardingCompleted ? 'Configurada' : 'Onboarding pendente' }}</q-item-label></q-item-section><q-item-section side><q-btn flat square class="collection-action-btn" color="primary" icon="open_in_new" :aria-label="'Abrir unidade ' + item.name" @click="continueToUnit({ establishmentId: item.id, nextStep: '/administration/onboarding/dados' })"><q-tooltip>Abrir unidade {{ item.name }}</q-tooltip></q-btn></q-item-section></q-item></q-list><q-btn color="primary" icon="add" label="Adicionar unidade" @click="openUnit(tenant)" /></q-card-section></q-card>
      </q-expansion-item>
    </q-list>
    <q-btn v-if="platform.lastIntentKey" flat color="primary" label="Verificar resultado do último envio" @click="recover" />

    <q-dialog v-model="showProvision" persistent><q-card style="width: 720px; max-width: 95vw"><q-form @submit="submitProvision"><q-card-section><h2 class="text-h6 q-my-none">Provisionar Tenant</h2></q-card-section><q-card-section class="q-gutter-md"><ProblemBanner :error="error" /><div class="text-subtitle2">Tenant</div><q-input v-model="form.tenantName" outlined label="Nome" :rules="[required]" /><q-input v-model="form.tenantPublicCode" outlined label="Código público" :rules="[required]" /><div class="text-subtitle2">Primeira unidade</div><q-input v-model="form.establishmentName" outlined label="Nome da unidade" :rules="[required]" /><q-input v-model="form.establishmentSlug" outlined label="Slug" :rules="[required]" /><q-input v-model="form.timeZoneId" outlined label="Fuso horário" :rules="[required]" /><div class="text-subtitle2">Primeiro Owner</div><q-input v-model="form.ownerName" outlined label="Nome" :rules="[required]" /><q-input v-model="form.ownerEmail" outlined type="email" label="E-mail" :rules="[required]" /><q-input v-model="form.temporaryPassword" outlined type="password" label="Senha temporária" :rules="[required]" autocomplete="new-password" /></q-card-section><q-card-actions align="right"><q-btn flat label="Cancelar" :disable="submitting" v-close-popup /><q-btn color="primary" unelevated icon="rocket_launch" type="submit" label="Provisionar" :loading="submitting" /></q-card-actions></q-form></q-card></q-dialog>
    <q-dialog v-model="showUnit" persistent><q-card style="width: 620px; max-width: 95vw"><q-form @submit="submitUnit"><q-card-section><h2 class="text-h6 q-my-none">Adicionar unidade</h2></q-card-section><q-card-section class="q-gutter-md"><ProblemBanner :error="error" /><q-input v-model="unit.name" outlined label="Nome" :rules="[required]" /><q-input v-model="unit.slug" outlined label="Slug" :rules="[required]" /><q-input v-model="unit.timeZoneId" outlined label="Fuso horário" :rules="[required]" /><q-select v-model="unit.ownerId" :options="platform.owners" option-value="id" option-label="name" emit-value map-options outlined label="Owner responsável" :rules="[required]" /></q-card-section><q-card-actions align="right"><q-btn flat label="Cancelar" :disable="submitting" v-close-popup /><q-btn color="primary" unelevated icon="add_business" type="submit" label="Adicionar" :loading="submitting" /></q-card-actions></q-form></q-card></q-dialog>
  </q-page>
</template>
