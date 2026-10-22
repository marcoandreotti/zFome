<script setup lang="ts">
import { useRouter } from 'vue-router'
import { useSessionStore } from '../modules/session/store'
import AppearanceControl from '../components/AppearanceControl.vue'
const router = useRouter()
const session = useSessionStore()
async function logout() { try { await session.logout() } finally { await router.replace('/login') } }
</script>
<template>
  <q-layout view="hHh lpR fFf" data-surface="platform">
    <q-header bordered class="platform-header">
      <a class="skip-link" href="#platform-content">Ir para o conteúdo</a>
      <q-toolbar class="q-px-md q-py-sm">
        <q-toolbar-title class="brand-logo"><img :src="'/brand/zfome-logo.png'" alt="zFome" /><span class="brand-logo__sr">zFome</span><span>PLATFORM</span></q-toolbar-title>
        <AppearanceControl />
        <q-btn flat no-caps label="Sair" @click="logout" />
      </q-toolbar>
    </q-header>
    <q-page-container><main id="platform-content" tabindex="-1"><router-view /></main></q-page-container>
  </q-layout>
</template>
<style scoped>
.platform-header {
  background: var(--zf-surface-raised);
  color: var(--zf-text-primary);
}
</style>
