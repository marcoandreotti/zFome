import { boot } from 'quasar/wrappers'
import { initializeAppearance } from '../themes/appearance'

export default boot(() => {
  initializeAppearance()
})
