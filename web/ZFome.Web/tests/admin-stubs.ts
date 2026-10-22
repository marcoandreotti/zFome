// Stubs preservam eventos e v-model; regras visuais do Quasar são verificadas no navegador.
export const adminStubs = {
  QBtn: {
    props: ['label', 'disable', 'type'],
    emits: ['click'],
    template:
      '<button :type="type || \'button\'" :disabled="disable" @click="$emit(\'click\')">{{ label }}</button>'
  },
  QInput: {
    props: ['label', 'modelValue'],
    emits: ['update:modelValue'],
    template:
      '<label>{{ label }}<input :value="modelValue" @input="$emit(\'update:modelValue\', $event.target.value)" /></label>'
  },
  QCheckbox: {
    props: ['label', 'modelValue'],
    emits: ['update:modelValue'],
    template:
      '<label>{{ label }}<input type="checkbox" :checked="modelValue" @change="$emit(\'update:modelValue\', $event.target.checked)" /></label>'
  },
  QToggle: {
    props: ['label', 'modelValue'],
    emits: ['update:modelValue'],
    template:
      '<label>{{ label }}<input type="checkbox" :checked="modelValue" @change="$emit(\'update:modelValue\', $event.target.checked)" /></label>'
  },
  QSelect: {
    props: ['label', 'modelValue', 'options'],
    emits: ['update:modelValue'],
    template:
      '<label>{{ label }}<select :value="modelValue" @change="$emit(\'update:modelValue\', $event.target.value)"><option v-for="item in options" :value="item.value || item.id">{{ item.label || item.name }}</option></select></label>'
  },
  QForm: {
    emits: ['submit'],
    template: '<form @submit.prevent="$emit(\'submit\')"><slot /></form>'
  },
  QDialog: {
    props: ['modelValue'],
    template: '<div v-if="modelValue"><slot /></div>'
  },
  QPagination: {
    emits: ['update:modelValue'],
    template:
      '<button @click="$emit(\'update:modelValue\', 2)">Página 2</button>'
  },
  QTabs: {
    name: 'QTabs',
    emits: ['update:modelValue'],
    template: '<div><slot /></div>'
  },
  QTab: true,
  QSpinner: true,
  QPage: { template: '<main><slot /></main>' },
  QCard: { template: '<section><slot /></section>' },
  QCardSection: { template: '<div><slot /></div>' },
  QCardActions: { template: '<div><slot /></div>' },
  QBanner: { template: '<div><slot /></div>' },
  QMarkupTable: { template: '<table><slot /></table>' }
}
