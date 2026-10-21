<script setup lang="ts">
import { computed } from 'vue'
import { useAppearance, type AppearanceMode } from '../themes/appearance'

const options: { value: AppearanceMode; label: string }[] = [
  { value: 'system', label: 'Sistema' },
  { value: 'light', label: 'Claro' },
  { value: 'dark', label: 'Escuro' }
]

const appearance = useAppearance()
const selectedLabel = computed(
  () => options.find(option => option.value === appearance.mode.value)?.label ?? 'Sistema'
)
</script>

<template>
  <div class="appearance-control">
    <q-btn
      flat
      no-caps
      class="appearance-control__trigger"
      :aria-label="'Aparência atual: ' + selectedLabel + '. Alterar tema'"
      aria-haspopup="menu"
    >
      <svg
        class="appearance-control__glyph"
        viewBox="0 0 24 24"
        fill="none"
        stroke="currentColor"
        stroke-width="1.8"
        stroke-linecap="round"
        stroke-linejoin="round"
        aria-hidden="true"
      >
        <circle cx="12" cy="12" r="4" />
        <path d="M12 2v2m0 16v2M4.93 4.93l1.42 1.42m11.3 11.3 1.42 1.42M2 12h2m16 0h2M4.93 19.07l1.42-1.42m11.3-11.3 1.42-1.42" />
      </svg>
      <span class="appearance-control__copy">
        <span>Aparência</span>
        <small>{{ selectedLabel }}</small>
      </span>
      <q-menu anchor="bottom right" self="top right">
        <q-list class="appearance-control__menu" role="radiogroup" aria-label="Aparência da aplicação">
          <div class="appearance-control__menu-title">Modo de cor</div>
          <q-item
            v-for="option in options"
            :key="option.value"
            clickable
            v-close-popup
            role="radio"
            :aria-checked="appearance.mode.value === option.value"
            :class="{ 'appearance-control__option--selected': appearance.mode.value === option.value }"
            @click="appearance.setMode(option.value)"
          >
            <q-item-section>{{ option.label }}</q-item-section>
            <q-item-section v-if="appearance.mode.value === option.value" side>
              <span class="appearance-control__selected-label">Selecionado</span>
            </q-item-section>
          </q-item>
        </q-list>
      </q-menu>
    </q-btn>
  </div>
</template>

<style scoped>
.appearance-control__trigger {
  min-height: 44px;
  padding-inline: 10px;
  border-radius: 8px;
  color: var(--zf-text-primary);
}
.appearance-control__glyph { width: 18px; height: 18px; flex: 0 0 18px; }
.appearance-control__copy { display: grid; gap: 2px; text-align: left; line-height: 1.1; }
.appearance-control__copy small { color: var(--zf-text-muted); font-size: .7rem; }
.appearance-control__menu {
  min-width: 220px;
  padding: 6px;
  background: var(--zf-surface-raised);
  color: var(--zf-text-primary);
}
.appearance-control__menu-title {
  padding: 10px 12px 8px;
  color: var(--zf-text-muted);
  font-size: .75rem;
  font-weight: 700;
  letter-spacing: .04em;
  text-transform: uppercase;
}
.appearance-control__menu :deep(.q-item) { min-height: 44px; border-radius: 7px; }
.appearance-control__menu :deep(.appearance-control__option--selected) {
  background: var(--zf-brand-soft);
  color: var(--zf-brand-primary-text);
  font-weight: 700;
}
.appearance-control__selected-label { color: var(--zf-text-muted); font-size: .72rem; font-weight: 500; }
</style>
