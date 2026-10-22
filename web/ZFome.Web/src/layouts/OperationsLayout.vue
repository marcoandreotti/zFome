<script setup lang="ts">
import { computed, nextTick, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useSessionStore } from '../modules/session/store'
import AppearanceControl from '../components/AppearanceControl.vue'

const session = useSessionStore()
const router = useRouter()
const route = useRoute()
const isKitchen = computed(() => route.path.endsWith('/kitchen'))
const isDelivery = computed(() => route.path.endsWith('/delivery'))

watch(
  () => route.fullPath,
  async () => {
    await nextTick()
    document.getElementById('operations-content')?.focus()
  }
)

async function logout() {
  try {
    await session.logout()
  } finally {
    await router.replace('/login')
  }
}
</script>

<template>
  <q-layout view="hHh lpR fFf" data-surface="operations" :class="{ 'operations-layout--kds': isKitchen }">
    <q-header bordered class="operations-header">
      <a class="skip-link" href="#operations-content">Ir para o conteúdo</a>
      <q-toolbar class="q-px-md q-py-sm operations-toolbar">
        <q-toolbar-title class="brand-logo">
          <img :src="'/brand/zfome-logo.png'" alt="zFome" /><span class="brand-logo__sr">zFome</span><span>{{ isKitchen ? 'COZINHA' : 'OPERAÇÃO' }}</span>
        </q-toolbar-title>
        <div class="operations-toolbar-controls">
          <q-select
            :model-value="session.unitId"
            :options="session.units"
            option-value="id"
            option-label="name"
            emit-value
            map-options
            dense
            outlined
            label="Unidade ativa"
            aria-label="Unidade ativa"
            :disable="!session.units.length"
            @update:model-value="session.selectUnit"
            class="operations-unit"
          />
          <nav aria-label="Navegação operacional" class="operations-navigation">
            <q-btn
              v-if="!isKitchen && session.can('order-kitchen')"
              flat no-caps label="Cozinha" to="/operations/kitchen"
              class="operations-nav-link"
            />
            <q-btn
              v-if="!isKitchen && session.can('order-delivery')"
              flat no-caps label="Entregas" to="/operations/delivery"
              :class="['operations-nav-link', { 'operations-nav-link--active': isDelivery }]"
            />
            <q-btn
              flat no-caps label="Pedidos" to="/operations"
              :class="['operations-nav-link', { 'operations-nav-link--active': !isKitchen && !isDelivery }]"
            />
            <q-btn
              v-if="!isKitchen && session.can('management')"
              outline no-caps label="Administração" to="/administration"
              class="operations-nav-link operations-nav-link--administration"
            />
          </nav>
          <AppearanceControl />
          <q-btn flat no-caps label="Sair" class="operations-logout" @click="logout" />
        </div>
      </q-toolbar>
    </q-header>
    <q-page-container>
      <main id="operations-content" tabindex="-1">
        <router-view v-if="session.context" :key="session.revision" />
      </main>
    </q-page-container>
  </q-layout>
</template>

<style scoped>
.operations-header {
  border-bottom: 2px solid var(--zf-brand-primary);
  background: var(--zf-navigation-background);
  color: var(--zf-navigation-text);
}

.operations-toolbar { min-height: 64px; gap: 20px; }
.operations-toolbar-controls { display: flex; align-items: center; gap: 12px; min-width: 0; }
.operations-navigation { display: flex; align-items: center; gap: 4px; min-width: 0; }

.operations-header :deep(.brand-logo) { color: var(--zf-navigation-text); }
.operations-header :deep(.brand-logo > span:last-child) { color: var(--zf-brand-primary); }
.operations-header :deep(.q-btn) { min-height: 44px; color: var(--zf-navigation-text); }
.operations-header :deep(.appearance-control__trigger) { color: var(--zf-navigation-text); }
.operations-header :deep(.operations-navigation .q-btn) { border-radius: 8px; }
.operations-header :deep(.operations-navigation .q-btn:hover) { background: rgb(255 255 255 / 9%); }
.operations-header :deep(.operations-nav-link--active) {
  background: rgb(255 255 255 / 12%);
  box-shadow: inset 0 -2px 0 var(--zf-brand-primary);
}
.operations-header :deep(.operations-nav-link--administration) {
  border-color: color-mix(in srgb, var(--zf-brand-primary) 70%, transparent);
}
.operations-header :deep(.operations-unit .q-field__label),
.operations-header :deep(.operations-unit .q-field__native),
.operations-header :deep(.operations-unit .q-field__marginal) { color: var(--zf-navigation-text); }
.operations-header :deep(.operations-unit .q-field__control:before) {
  border-color: color-mix(in srgb, var(--zf-navigation-muted) 55%, transparent);
}
.operations-header :deep(.operations-unit .q-field__control:hover:before) { border-color: var(--zf-brand-primary); }

@media (max-width: 1000px) {
  .operations-toolbar { flex-wrap: wrap; gap: 8px 16px; }
  .operations-toolbar-controls { flex: 1 1 100%; flex-wrap: wrap; }
}

@media (max-width: 700px) {
  .operations-toolbar { padding-block: 10px; }
  .operations-toolbar-controls { gap: 8px; }
  .operations-unit { flex: 1 1 100%; width: 100%; }
  .operations-navigation { flex: 1 1 auto; overflow-x: auto; }
  .operations-header :deep(.operations-navigation .q-btn) { flex: 0 0 auto; }
  .operations-logout { flex: 0 0 auto; }
}
</style>
