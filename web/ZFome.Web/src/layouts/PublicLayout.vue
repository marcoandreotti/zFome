<script setup lang="ts">
import AppearanceControl from '../components/AppearanceControl.vue'
</script>

<template>
  <q-layout view="hHh lpR fFf" data-surface="public">
    <a class="skip-link" href="#main-content">Pular para o conteúdo</a>
    <q-header class="public-header"><q-toolbar class="q-px-md"><q-toolbar-title>zFome</q-toolbar-title><AppearanceControl /></q-toolbar></q-header>
    <q-page-container><router-view /></q-page-container>
  </q-layout>
</template>
