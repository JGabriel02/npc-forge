import { isExhausted, usageLabel } from './ai-usage';

const usage = (used: number, limit: number) => ({ used, limit, resetsAt: '2026-10-30T00:00:00Z' });

describe('ai-usage', () => {
  it('without limit shows only how many were generated today', () => {
    expect(usageLabel(usage(0, 0))).toBe('Nenhuma geração hoje');
    expect(usageLabel(usage(1, 0))).toBe('1 geração hoje');
    expect(usageLabel(usage(7, 0))).toBe('7 gerações hoje');
  });

  it('with limit shows used of limit', () => {
    expect(usageLabel(usage(3, 5))).toBe('3 de 5 gerações hoje');
  });

  it('is never exhausted without limit', () => {
    expect(isExhausted(usage(100, 0))).toBe(false);
  });

  it('is exhausted when the limit is reached', () => {
    expect(isExhausted(usage(4, 5))).toBe(false);
    expect(isExhausted(usage(5, 5))).toBe(true);
  });

  it('is not exhausted while usage is unknown', () => {
    expect(isExhausted(null)).toBe(false);
  });
});
