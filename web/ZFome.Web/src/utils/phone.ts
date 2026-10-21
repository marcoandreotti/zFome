const MOBILE_MASK = '(##) #####-####'
const LANDLINE_MASK = '(##) ####-####'
const NATIONAL_MAX_LENGTH = 11

/**
 * Converte um telefone brasileiro para os dígitos nacionais (DDD + número).
 *
 * Aceita formatação livre, código do país (+55) e prefixo de longa distância
 * (0), removendo-os apenas quando o total é reconhecível. Assim, um DDD que
 * começa com 55 (por exemplo 55 99999-9999) não perde os dois primeiros dígitos.
 */
export function canonicalPhone(value: string): string {
  let digits = value.replace(/\D/g, '')
  if (digits.startsWith('55')) {
    const withoutCountryCode = digits.slice(2)
    if (digits.length === 12 || digits.length === 13)
      digits = withoutCountryCode
    else if ((withoutCountryCode.length === 11 || withoutCountryCode.length === 12)
      && withoutCountryCode.startsWith('0'))
      digits = withoutCountryCode
  }
  if ((digits.length === 11 || digits.length === 12) && digits.startsWith('0'))
    digits = digits.slice(1)
  return digits
}

/**
 * Limita o valor ao tamanho nacional depois de remover os prefixos. Usado na
 * colagem: o prefixo é descartado antes do corte, nunca no lugar do DDD.
 */
export function limitPhone(value: string): string {
  return canonicalPhone(value).slice(0, NATIONAL_MAX_LENGTH)
}

/**
 * Máscara progressiva do Quasar. Celulares usam cinco dígitos antes do hífen;
 * fixos usam quatro. O terceiro dígito define o tipo já durante a digitação.
 */
export function phoneMask(value: string): string {
  return canonicalPhone(value).slice(2).startsWith('9') ? MOBILE_MASK : LANDLINE_MASK
}

/** Devolve a mensagem de validação do telefone ou string vazia quando válido. */
export function phoneValidationMessage(value: string): string {
  const digits = canonicalPhone(value)
  if (!digits) return 'Informe o telefone.'
  if (digits.length < 10) return 'Informe DDD e telefone completo.'
  if (digits.length > NATIONAL_MAX_LENGTH) return 'Informe DDD e telefone com 8 ou 9 dígitos.'
  const ddd = Number(digits.slice(0, 2))
  if (ddd < 11 || ddd > 99) return 'Informe um DDD válido.'
  const subscriber = digits.slice(2)
  if (subscriber.length === 9 && !subscriber.startsWith('9'))
    return 'Celular deve começar com 9 após o DDD.'
  if (subscriber.length === 8 && !'2345'.includes(subscriber[0] ?? ''))
    return 'Informe um telefone fixo válido.'
  return ''
}
