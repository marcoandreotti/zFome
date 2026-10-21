<script setup lang="ts">
import { ref, onUnmounted } from 'vue'
import { useRouter } from 'vue-router'
import { api, ApiError } from '../../http/client'
import { useSessionStore } from './store'
import ProblemBanner from '../../components/ProblemBanner.vue'
const session = useSessionStore()
const router = useRouter()
const currentPassword = ref('')
const newPassword = ref('')
const confirmation = ref('')
const busy = ref(false)
const error = ref<unknown>(null)
async function submit() {
  busy.value = true
  error.value = null
  try {
    await api.post('/api/auth/change-password', {
      currentPassword: currentPassword.value,
      newPassword: newPassword.value
    })
    session.clear()
    await router.replace({ path: '/login', query: { passwordChanged: 'true' } })
  } catch (failure) {
    error.value = failure
  } finally {
    busy.value = false
  }
}
async function leave() {
  try {
    await session.logout()
  } finally {
    await router.replace('/login')
  }
}
onUnmounted(() => {
  currentPassword.value = ''
  newPassword.value = ''
  confirmation.value = ''
})
</script>
<template>
  <q-page class="auth-page">
    <q-card flat bordered class="auth-card"
      ><q-card-section>
        <div class="brand-wordmark">zFome</div>
        <h1 class="text-h5">Defina sua senha definitiva</h1>
        <p>
          Antes de administrar a plataforma, substitua a senha temporária.
          Depois, entre novamente.
        </p>
        <ProblemBanner :error="error" />
        <q-form class="q-gutter-md" @submit="submit">
          <q-input
            v-model="currentPassword"
            outlined
            type="password"
            label="Senha temporária"
            autocomplete="current-password"
            :rules="[(value) => !!value || 'Campo obrigatório']"
            :disable="busy"
          />
          <q-input
            v-model="newPassword"
            outlined
            type="password"
            label="Nova senha"
            autocomplete="new-password"
            :rules="[
              (value) =>
                (value.length >= 10 && value.length <= 200) ||
                'Use de 10 a 200 caracteres',
              (value) =>
                value !== currentPassword || 'Escolha uma senha diferente'
            ]"
            :error="error instanceof ApiError && !!error.field('newPassword')"
            :error-message="
              error instanceof ApiError ? error.field('newPassword') : undefined
            "
            :disable="busy"
          />
          <q-input
            v-model="confirmation"
            outlined
            type="password"
            label="Confirme a nova senha"
            autocomplete="new-password"
            :rules="[
              (value) => value === newPassword || 'As senhas não coincidem'
            ]"
            :disable="busy"
          />
          <q-btn
            type="submit"
            icon="save"
            label="Salvar nova senha"
            color="primary"
            unelevated
            :loading="busy"
            class="full-width"
          />
          <q-btn
            flat
            label="Sair"
            :disable="busy"
            @click="leave"
            class="full-width"
          />
        </q-form> </q-card-section
    ></q-card>
  </q-page>
</template>
