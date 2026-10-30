// Tipos que espelham os DTOs da API (.NET). Se um DTO mudar no backend, este arquivo muda junto.

export interface MeResponse {
  id: string;
  email: string;
  displayName: string;
  /** Conta de visitante (somente leitura). */
  isDemo: boolean;
}

export interface LoginResponse {
  token: string;
  expiresAt: string;
}

export interface CampaignRequest {
  name: string;
  description: string | null;
  setting: string | null;
}

export interface CampaignDto {
  id: string;
  name: string;
  description: string | null;
  setting: string | null;
  npcCount: number;
  createdAt: string;
  updatedAt: string;
}

export type CreatureSize = 'Tiny' | 'Small' | 'Medium' | 'Large' | 'Huge' | 'Gargantuan';

export interface StatBlockFeatureDto {
  name: string;
  description: string;
}

export interface StatBlockActionDto extends StatBlockFeatureDto {
  attackBonus: number | null;
  damage: string | null;
}

export interface StatBlockRequest {
  size: CreatureSize;
  creatureType: string;
  alignment: string;
  armorClass: number;
  hitPoints: number;
  hitDice: string;
  speed: string;
  str: number;
  dex: number;
  con: number;
  int: number;
  wis: number;
  cha: number;
  challengeRating: string;
  savingThrows: string | null;
  skills: string | null;
  senses: string | null;
  languages: string | null;
  traits: StatBlockFeatureDto[];
  actions: StatBlockActionDto[];
  reactions: StatBlockFeatureDto[];
}

export interface AbilityModifiers {
  str: number;
  dex: number;
  con: number;
  int: number;
  wis: number;
  cha: number;
}

export interface StatBlockDto extends StatBlockRequest {
  modifiers: AbilityModifiers;
  proficiencyBonus: number;
}

export interface UpdateNpcRequest {
  campaignId: string | null;
  name: string;
  race: string | null;
  occupation: string | null;
  appearance: string | null;
  personality: string | null;
  background: string | null;
  notes: string | null;
}

export interface CreateNpcRequest extends UpdateNpcRequest {
  statBlock: StatBlockRequest | null;
}

export interface NpcDto extends UpdateNpcRequest {
  id: string;
  statBlock: StatBlockDto | null;
  createdAt: string;
  updatedAt: string;
}

export interface NpcSummaryDto {
  id: string;
  name: string;
  race: string | null;
  occupation: string | null;
  campaignId: string | null;
  campaignName: string | null;
  hasStatBlock: boolean;
  challengeRating: string | null;
  updatedAt: string;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}

/** Um NPC no arquivo de exportação: só o conteúdo (sem id, campanha ou datas). */
export interface ExportedNpc {
  name: string;
  race: string | null;
  occupation: string | null;
  appearance: string | null;
  personality: string | null;
  background: string | null;
  notes: string | null;
  statBlock: StatBlockRequest | null;
}

/** Arquivo de exportação de NPCs ("npcforge-npcs"). */
export interface NpcExportFile {
  format: string;
  version: number;
  exportedAt: string;
  npcs: ExportedNpc[];
}

export const EXPORT_FORMAT = 'npcforge-npcs';
export const EXPORT_VERSION = 1;
/** Tamanho máximo do arquivo de importação (igual ao NpcExportFile.MaxFileBytes da API). */
export const EXPORT_MAX_BYTES = 20 * 1024 * 1024;

/** NPC sugerido pela IA (não salvo). */
export interface NpcDraft {
  name: string;
  race: string | null;
  occupation: string | null;
  appearance: string | null;
  personality: string | null;
  background: string | null;
  statBlock: StatBlockRequest | null;
}

export interface GenerateNpcRequest {
  prompt: string;
  campaignId: string | null;
  withStatBlock: boolean;
  challengeRating: string | null;
}

export interface AiUsageDto {
  used: number;
  limit: number;
  resetsAt: string;
}

export interface GenerateNpcResponse {
  draft: NpcDraft;
  warnings: string[];
  usage: AiUsageDto;
}

/** Formato padrão de erro da API (RFC 9457). */
export interface ProblemDetails {
  title?: string;
  status?: number;
  errors?: Record<string, string[]>;
}

export const CHALLENGE_RATINGS: readonly string[] = [
  '0', '1/8', '1/4', '1/2',
  ...Array.from({ length: 30 }, (_, i) => String(i + 1)),
];

export const CREATURE_SIZES: readonly { value: CreatureSize; label: string }[] = [
  { value: 'Tiny', label: 'Miúdo' },
  { value: 'Small', label: 'Pequeno' },
  { value: 'Medium', label: 'Médio' },
  { value: 'Large', label: 'Grande' },
  { value: 'Huge', label: 'Enorme' },
  { value: 'Gargantuan', label: 'Imenso' },
];

export type AbilityKey = keyof AbilityModifiers;

export const ABILITIES: readonly { key: AbilityKey; label: string; short: string }[] = [
  { key: 'str', label: 'Força', short: 'FOR' },
  { key: 'dex', label: 'Destreza', short: 'DES' },
  { key: 'con', label: 'Constituição', short: 'CON' },
  { key: 'int', label: 'Inteligência', short: 'INT' },
  { key: 'wis', label: 'Sabedoria', short: 'SAB' },
  { key: 'cha', label: 'Carisma', short: 'CAR' },
];

/** Mesmo limite do backend (NpcLimits.StatBlockListItems). */
export const STAT_BLOCK_LIST_MAX = 30;
