import type { PublicContext } from './types'

const defaults = {
  primary: '#f97316', onPrimary: '#111827', secondary: '#1f2937',
  background: '#f8fafc', text: '#374151', darkSurface: '#1f2937',
  darkMuted: '#cbd5e1'
}
function hex(value: string) {
  if (!/^#[0-9a-f]{6}$/i.test(value)) return null
  return [1, 3, 5].map(index => Number.parseInt(value.slice(index, index + 2), 16) / 255)
}
function luminance(value: string) {
  const rgb = hex(value)
  if (!rgb) return null
  const linear = rgb.map(channel => channel <= 0.03928
    ? channel / 12.92 : ((channel + 0.055) / 1.055) ** 2.4)
  return 0.2126 * linear[0]! + 0.7152 * linear[1]! + 0.0722 * linear[2]!
}
export function contrast(first: string, second: string) {
  const a = luminance(first)
  const b = luminance(second)
  if (a === null || b === null) return 0
  return (Math.max(a, b) + 0.05) / (Math.min(a, b) + 0.05)
}
export function publicThemeSurface() {
  return document.querySelector<HTMLElement>('[data-surface="public"]')
}
export function applyPublicTheme(value: PublicContext, surface = publicThemeSurface()) {
  if (!surface) return false
  const background = hex(value.theme.backgroundColor) ? value.theme.backgroundColor : defaults.background
  const fallbackText = contrast(background, defaults.text) >= 4.5 ? defaults.text : '#111827'
  const text = contrast(background, value.theme.textColor) >= 4.5 ? value.theme.textColor : fallbackText
  const suppliedPrimary = hex(value.theme.primaryColor) ? value.theme.primaryColor : defaults.primary
  const suppliedOnPrimary = contrast(suppliedPrimary, defaults.onPrimary) >= 4.5
    ? defaults.onPrimary
    : '#ffffff'
  const primary = contrast(suppliedPrimary, suppliedOnPrimary) >= 4.5 ? suppliedPrimary : defaults.primary
  const onPrimary = primary === suppliedPrimary ? suppliedOnPrimary : defaults.onPrimary
  const primaryText = contrast(primary, '#ffffff') >= 4.5 ? primary : '#c2410c'
  const fallbackSecondary = contrast(defaults.secondary, background) >= 3 ? defaults.secondary : text
  const secondary = contrast(value.theme.secondaryColor, background) >= 3 ? value.theme.secondaryColor : fallbackSecondary
  const primaryTextDark = contrast(primary, defaults.darkSurface) >= 4.5 ? primary : '#fdba74'
  const secondaryDark = contrast(value.theme.secondaryColor, defaults.darkSurface) >= 4.5
    ? value.theme.secondaryColor
    : defaults.darkMuted
  surface.style.setProperty('--zf-public-theme-background', background)
  surface.style.setProperty('--zf-public-theme-text', text)
  surface.style.setProperty('--zf-public-brand-primary', primary)
  surface.style.setProperty('--zf-public-brand-primary-text', primaryText)
  surface.style.setProperty('--zf-public-brand-primary-text-dark', primaryTextDark)
  surface.style.setProperty('--zf-public-brand-on-primary', onPrimary)
  surface.style.setProperty('--zf-public-brand-secondary', secondary)
  surface.style.setProperty('--zf-public-brand-secondary-dark', secondaryDark)
  surface.style.setProperty('--zf-public-font-family', /^[\w ,'-]+$/.test(value.theme.fontFamily)
    ? value.theme.fontFamily : 'system-ui, sans-serif')
  return true
}
