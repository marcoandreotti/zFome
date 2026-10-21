<script setup lang="ts">
import { ref, onUnmounted } from 'vue'
import { useRouter } from 'vue-router'
import { api, ApiError } from '../../http/client'
import { useSessionStore } from './store'
import { sessionLandingPath } from '../../router/access'
import ProblemBanner from '../../components/ProblemBanner.vue'
const router = useRouter()
const session = useSessionStore()
const contextCode = ref('')
const email = ref('')
const password = ref('')
const code = ref('')
const challengeId = ref('')
const busy = ref(false)
const error = ref<unknown>(null)
const required = (value: string) => !!value.trim() || 'Campo obrigatório'
const field = (name: string) =>
  error.value instanceof ApiError ? error.value.field(name) : undefined

async function submit() {
  busy.value = true
  error.value = null
  try {
    if (!challengeId.value) {
      const response = await api.post<{ challengeId: string }>(
        '/api/auth/begin',
        {
          contextCode: contextCode.value,
          email: email.value,
          password: password.value
        }
      )
      challengeId.value = response.data.challengeId
      password.value = ''
    } else {
      await api.post('/api/auth/complete', {
        challengeId: challengeId.value,
        code: code.value
      })
      code.value = ''
      await session.hydrate()
      await router.replace(sessionLandingPath(session.context))
    }
  } catch (failure) {
    error.value = failure
  } finally {
    busy.value = false
  }
}
function restart() {
  challengeId.value = ''
  code.value = ''
  error.value = null
}
onUnmounted(() => {
  password.value = ''
  code.value = ''
})
</script>
<template>
  <q-page class="auth-page">
    <q-card flat bordered class="auth-card">
      <q-card-section>
        <div class="brand-wordmark">zFome <span>ADMIN</span></div>
        <h1 class="text-h4 q-mt-lg q-mb-sm">
          {{ challengeId ? 'Confira seu e-mail' : 'Bem-vindo de volta' }}
        </h1>
        <p class="text-grey-8">
          {{
            challengeId
              ? 'Digite o código de seis dígitos enviado ao seu e-mail.'
              : 'Entre para administrar seu estabelecimento.'
          }}
        </p>
        <ProblemBanner :error="error" />
        <q-form @submit="submit" class="q-gutter-md">
          <template v-if="!challengeId">
            <q-input
              v-model="contextCode"
              outlined
              label="Código do Tenant ou da plataforma"
              autocomplete="organization"
              :rules="[required]"
              :error="!!field('contextCode')"
              :error-message="field('contextCode')"
              :disable="busy"
            />
            <q-input
              v-model="email"
              outlined
              type="email"
              label="E-mail"
              autocomplete="username"
              :rules="[required]"
              :error="!!field('email')"
              :error-message="field('email')"
              :disable="busy"
            />
            <q-input
              v-model="password"
              outlined
              type="password"
              label="Senha"
              autocomplete="current-password"
              :rules="[required]"
              :disable="busy"
            />
          </template>
          <q-input
            v-else
            v-model="code"
            outlined
            label="Código de verificação"
            inputmode="numeric"
            autocomplete="one-time-code"
            maxlength="6"
            autofocus
            :rules="[
              (value) => /^\d{6}$/.test(value) || 'Informe os seis dígitos'
            ]"
            :disable="busy"
          />
          <q-btn
            unelevated
            color="primary"
            :icon="challengeId ? 'verified' : 'login'"
            type="submit"
            :label="challengeId ? 'Confirmar acesso' : 'Continuar'"
            :loading="busy"
            class="full-width"
            size="lg"
          />
          <q-btn
            v-if="challengeId"
            flat
            no-caps
            label="Reiniciar login / solicitar outro código"
            :disable="busy"
            @click="restart"
            class="full-width"
          />
        </q-form>
        <p class="text-caption text-grey-7 q-mt-lg">
          A senha e a verificação por e-mail protegem o acesso. O código do
          Tenant apenas identifica o contexto.
        </p>
      </q-card-section>
    </q-card>
  </q-page>
</template>
