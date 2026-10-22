<script setup lang="ts">
import { computed } from 'vue'
import { useRoute } from 'vue-router'
import AppearanceControl from '../components/AppearanceControl.vue'

const route = useRoute()
const isFoundation = computed(() => route.path === '/')
</script>

<template>
  <q-layout view="hHh lpR fFf" data-surface="public">
    <a class="skip-link" href="#main-content">Pular para o conteúdo</a>
    <q-header v-if="!isFoundation" class="public-header"><q-toolbar class="q-px-md"><q-toolbar-title class="brand-logo"><img :src="'/brand/zfome-logo.png'" alt="zFome" /><span class="brand-logo__sr">zFome</span></q-toolbar-title><AppearanceControl /></q-toolbar></q-header>
    <q-page-container><router-view /></q-page-container>
  </q-layout>
</template>
