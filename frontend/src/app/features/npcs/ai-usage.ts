import { AiUsageDto } from '../../core/api/api.models';

// limit = 0 significa "sem limite" (padrão da versão local: a IA roda no computador do usuário).

export function usageLabel(usage: AiUsageDto): string {
  if (usage.limit > 0) return `${usage.used} de ${usage.limit} gerações hoje`;
  if (usage.used === 0) return 'Nenhuma geração hoje';
  return usage.used === 1 ? '1 geração hoje' : `${usage.used} gerações hoje`;
}

export function isExhausted(usage: AiUsageDto | null): boolean {
  return usage !== null && usage.limit > 0 && usage.used >= usage.limit;
}
