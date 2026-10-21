import { phoneValidationMessage } from '../../utils/phone'
import type { Address, ServiceType } from './types'

export function checkoutValidation(
  serviceType: ServiceType,
  paymentMethodId: string,
  customer: { name: string; phone: string },
  address: Address
) {
  if (!paymentMethodId) return 'Selecione a forma de pagamento.'
  if (serviceType !== 'Table' && (!customer.name.trim() || !customer.phone.trim()))
    return 'Informe nome e telefone.'
  if (serviceType !== 'Table') {
    const phone = phoneValidationMessage(customer.phone)
    if (phone) return phone
  }
  if (serviceType === 'Delivery' &&
    (!address.street.trim() || !address.number.trim() || !address.neighborhood.trim() ||
      !address.city.trim() || !address.state.trim() || !address.postalCode.trim()))
    return 'Preencha o endereço de entrega.'
  return ''
}
