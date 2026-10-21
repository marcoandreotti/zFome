<script setup lang="ts">
import { useSessionStore } from './store'
import { useRouter } from 'vue-router'
const session = useSessionStore()
const router = useRouter()
async function leave() {
  try {
    await session.logout()
  } finally {
    await router.replace('/login')
  }
}
</script>
<template>
  <q-page padding class="column items-center justify-center text-center">
    <h1 class="text-h4">Acesso não permitido</h1>
    <p>Seu perfil não possui a capacidade necessária para esta área.</p>
    <q-btn color="primary" label="Entrar com outra conta" @click="leave" />
  </q-page>
</template>
