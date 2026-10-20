import { describe, expect, it } from 'vitest'
import {
  canonicalPhone,
  limitPhone,
  phoneMask,
  phoneValidationMessage
} from '../src/utils/phone'

describe('telefone brasileiro', () => {
  it.each([
    ['+55019999999999', '19999999999'],
    ['+55 (0) 19 99999-9999', '19999999999'],
    ['5511999999999', '11999999999'],
    ['011 3333-4444', '1133334444'],
    ['(11) 99999-9999', '11999999999'],
    ['11 3333-4444', '1133334444'],
    ['55 99999-9999', '55999999999'],
    ['', '']
  ])('canonicaliza %s como %s', (input, expected) => {
    expect(canonicalPhone(input)).toBe(expected)
  })

  it('não trunca o DDD ao colar com código do país e prefixo de longa distância', () => {
    expect(limitPhone('+55019999999999')).toBe('19999999999')
    expect(limitPhone('+55 (0) 19 99999-9999')).toBe('19999999999')
    expect(limitPhone('+55 (11) 3333-4444')).toBe('1133334444')
  })

  it('escolhe a máscara conforme fixo ou celular já durante a digitação', () => {
    expect(phoneMask('')).toBe('(##) ####-####')
    expect(phoneMask('11')).toBe('(##) ####-####')
    expect(phoneMask('119')).toBe('(##) #####-####')
    expect(phoneMask('113')).toBe('(##) ####-####')
    expect(phoneMask('+55 (0) 19 99999-9999')).toBe('(##) #####-####')
  })

  it('valida DDD, telefone fixo e celular', () => {
    expect(phoneValidationMessage('11999999999')).toBe('')
    expect(phoneValidationMessage('+55 (0) 19 99999-9999')).toBe('')
    expect(phoneValidationMessage('1133334444')).toBe('')
    expect(phoneValidationMessage('1199999999')).toBe('Informe um telefone fixo válido.')
    expect(phoneValidationMessage('11799999999')).toBe('Celular deve começar com 9 após o DDD.')
    expect(phoneValidationMessage('0099999999')).toBe('Informe um DDD válido.')
    expect(phoneValidationMessage('119999999')).toBe('Informe DDD e telefone completo.')
    expect(phoneValidationMessage('119999999999')).toBe(
      'Informe DDD e telefone com 8 ou 9 dígitos.'
    )
    expect(phoneValidationMessage('abc')).toBe('Informe o telefone.')
    expect(phoneValidationMessage('')).toBe('Informe o telefone.')
  })
})
