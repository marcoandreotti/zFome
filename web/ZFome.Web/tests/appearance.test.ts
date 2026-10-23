import { beforeEach, describe, expect, it, vi } from 'vitest'

const setDarkMode = vi.fn()

vi.mock('quasar', () => ({
  Dark: { set: setDarkMode }
}))

describe('preferência de aparência da identidade zFome', () => {
  beforeEach(() => {
    vi.resetModules()
    vi.clearAllMocks()
    localStorage.clear()
    document.documentElement.removeAttribute('data-theme')
    window.matchMedia = vi.fn().mockReturnValue({
      matches: false,
      addEventListener: vi.fn()
    })
  })

  it('não migra a preferência armazenada pela identidade anterior', async () => {
    localStorage.setItem('zfome.appearance-mode', 'dark')

    const appearance = await import('../src/themes/appearance')
    appearance.initializeAppearance()

    expect(appearance.useAppearance().mode.value).toBe('system')
    expect(localStorage.getItem('zfome.appearance-mode')).toBeNull()
    expect(document.documentElement.dataset.theme).toBe('light')
    expect(setDarkMode).toHaveBeenCalledWith(false)
  })
})
