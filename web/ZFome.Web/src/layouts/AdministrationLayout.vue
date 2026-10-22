<script setup lang="ts">
import { computed, nextTick, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useSessionStore } from '../modules/session/store'
import AppearanceControl from '../components/AppearanceControl.vue'
const drawer = ref(false)
const session = useSessionStore()
const router = useRouter()
const route = useRoute()
const activeUnitName = computed(
  () => session.units.find((unit) => unit.id === session.unitId)?.name ?? 'Sem unidade ativa'
)
watch(
  () => [route.path, session.revision],
  async () => {
    drawer.value = false
    await nextTick()
    document.getElementById('admin-content')?.focus()
  }
)
async function returnToPlatform() {
  session.clear()
  await router.push('/platform')
}
async function logout() {
  try {
    await session.logout()
  } finally {
    await router.replace('/login')
  }
}
</script>
<template>
  <q-layout view="hHh lpR fFf" data-surface="administration">
    <q-header bordered class="administration-header">
      <a class="skip-link" href="#admin-content">Ir para o conteúdo</a>
      <q-toolbar class="q-px-md q-py-sm administration-toolbar">
        <q-btn flat round aria-label="Abrir navegação" @click="drawer = !drawer"
          ><span aria-hidden="true">☰</span></q-btn
        >
        <q-toolbar-title class="brand-logo"
          ><img :src="'/brand/zfome-logo.png'" alt="zFome" /><span class="brand-logo__sr">zFome</span><span>ADMIN</span></q-toolbar-title
        >
        <span class="administration-active-unit" aria-label="Unidade ativa">
          {{ activeUnitName }}
        </span>
        <AppearanceControl />
        <q-btn flat no-caps label="Sair" @click="logout" />
      </q-toolbar>
    </q-header>
    <q-drawer
      v-model="drawer"
      show-if-above
      bordered
      :width="256"
      class="administration-drawer"
    >
      <nav aria-label="Administração" class="administration-navigation q-pa-md">
        <div class="text-overline text-grey-7 q-mb-md">ÁREA ADMINISTRATIVA</div>
        <q-select
          :model-value="session.unitId"
          :options="session.units"
          option-value="id"
          option-label="name"
          emit-value
          map-options
          outlined
          label="Unidade ativa"
          aria-label="Unidade ativa"
          :disable="!session.units.length"
          @update:model-value="session.selectUnit"
          class="q-mb-lg"
        />
        <q-list>
          <q-item
            v-if="session.can('order-read')"
            clickable
            to="/operations"
            class="admin-nav-link rounded-borders admin-nav-link--operations"
          >
            <q-item-section>Voltar às operações</q-item-section>
          </q-item>
          <q-item
            v-if="session.context?.isPlatformUser"
            clickable
            @click="returnToPlatform"
            class="admin-nav-link rounded-borders"
          >
            <q-item-section>Voltar à plataforma</q-item-section>
          </q-item>
          <q-item v-if="session.can('administration')" clickable to="/administration/onboarding" active-class="admin-nav-item--active" class="admin-nav-link rounded-borders"><q-item-section>Configurar unidade</q-item-section></q-item>
          <q-item
            v-if="session.can('management')"
            clickable
            to="/administration/catalog"
            active-class="admin-nav-item--active"
            class="admin-nav-link rounded-borders"
            ><q-item-section>Catálogo</q-item-section></q-item
          >
          <q-item
            v-if="session.can('administration')"
            clickable
            to="/administration/availability"
            active-class="admin-nav-item--active"
            class="admin-nav-link rounded-borders"
            ><q-item-section>Disponibilidade</q-item-section></q-item
          >
          <q-item v-if="session.can('management')" clickable to="/administration/delivery-regions" active-class="admin-nav-item--active" class="admin-nav-link rounded-borders"><q-item-section>Regiões de entrega</q-item-section></q-item>
          <q-item
            v-if="session.can('administration')"
            clickable
            to="/administration/users"
            active-class="admin-nav-item--active"
            class="admin-nav-link rounded-borders"
            ><q-item-section>Usuários</q-item-section></q-item
          >
          <q-item
            v-if="session.can('customer-operations')"
            clickable
            to="/administration/customers"
            active-class="admin-nav-item--active"
            class="admin-nav-link rounded-borders"
            ><q-item-section>Clientes</q-item-section></q-item
          >
          <q-item
            v-if="session.can('promotion-management')"
            clickable
            to="/administration/coupons"
            active-class="admin-nav-item--active"
            class="admin-nav-link rounded-borders"
            ><q-item-section>Cupons</q-item-section></q-item
          >
          <q-item
            v-if="session.can('payment-management')"
            clickable
            to="/administration/payment-methods"
            active-class="admin-nav-item--active"
            class="admin-nav-link rounded-borders"
            ><q-item-section>Formas de pagamento</q-item-section></q-item
          >
          <q-item
            v-if="session.can('management')"
            clickable
            to="/administration/communications"
            active-class="admin-nav-item--active"
            class="admin-nav-link rounded-borders"
            ><q-item-section>Notificações</q-item-section></q-item
          >
          <q-item
            v-if="session.can('management')"
            clickable
            to="/administration/reviews"
            active-class="admin-nav-item--active"
            class="admin-nav-link rounded-borders"
            ><q-item-section>Avaliações</q-item-section></q-item
          >
          <q-item
            v-if="session.can('management')"
            clickable
            to="/administration"
            exact
            active-class="admin-nav-item--active"
            class="admin-nav-link rounded-borders"
            ><q-item-section>Visão geral</q-item-section></q-item
          >
          <q-item
            v-if="session.can('management')"
            clickable
            to="/administration/foundation"
            active-class="admin-nav-item--active"
            class="admin-nav-link rounded-borders"
            ><q-item-section>Fundação do projeto</q-item-section></q-item
          >
        </q-list>
      </nav>
    </q-drawer>
    <q-page-container
      ><main id="admin-content" tabindex="-1">
        <router-view v-if="session.context" :key="session.revision" /></main
    ></q-page-container>
  </q-layout>
</template>

<style scoped>
:global(.q-drawer__content.administration-drawer) {
  background-color: var(--zf-navigation-background) !important;
  color: var(--zf-navigation-text);
}
.administration-navigation :deep(.text-overline) { color: var(--zf-navigation-muted) !important; }
.administration-navigation :deep(.q-item) { color: var(--zf-navigation-text); }
.administration-navigation :deep(.q-field__label),
.administration-navigation :deep(.q-field__native),
.administration-navigation :deep(.q-field__marginal) { color: var(--zf-navigation-text); }
.administration-navigation :deep(.q-field--outlined .q-field__control:before) { border-color: color-mix(in srgb, var(--zf-navigation-muted) 45%, var(--zf-navigation-background)); }
.administration-navigation :deep(.q-field--outlined .q-field__control:hover:before) { border-color: var(--zf-brand-primary); }
.administration-navigation :deep(.admin-nav-link) { min-height: 44px; margin: 3px 0; }
.administration-navigation :deep(.admin-nav-link:hover) { background: rgb(255 255 255 / 8%); }
.administration-navigation :deep(.admin-nav-item--active) {
  background: color-mix(in srgb, var(--zf-admin-accent) 24%, var(--zf-navigation-background));
  color: #fff !important;
  font-weight: 700;
}
.administration-header {
  margin: 12px 16px 0;
  overflow: hidden;
  border: 1px solid var(--zf-border-subtle);
  border-radius: var(--zf-border-radius);
  background: var(--zf-surface-raised);
  color: var(--zf-text-primary);
  box-shadow: 0 3px 14px rgb(31 41 55 / 6%);
}
@media (max-width: 600px) {
  .administration-header { margin: 8px 8px 0; }
}
</style>
