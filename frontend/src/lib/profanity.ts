/**
 * Client-side profanity check for tag inputs and short text fields.
 * Mirrors the server-side ProfanityFilterService techniques:
 *   1. Leetspeak normalization  (@→a, 3→e, 0→o …)
 *   2. Separator-tolerant matching (f.u.c.k, f-u-c-k, f u c k)
 *   3. Cyrillic↔Latin homoglyphs (а→a, е→e, о→o …)
 *   4. Full word list matching
 *
 * The word list is shared with the backend via packages/shared/profanity_words.json.
 * The real defense is on the server — this is a UX guard to give
 * instant feedback before a round-trip.
 */

import PROFANITY_WORDS from "./profanity_words.json"

// ─── Leetspeak map ───────────────────────────────────────────────────────────

const LEET_MAP: Record<string, string> = {
  "@": "a",
  "4": "a",
  "3": "e",
  "1": "i",
  "!": "i",
  "0": "o",
  "$": "s",
  "5": "s",
  "7": "t",
  "+": "t",
  "8": "b",
  "9": "g",
  "|": "l",
}

// ─── Cyrillic → Latin homoglyph map ─────────────────────────────────────────
// People mix Cyrillic look-alikes into Latin words (and vice-versa) to bypass.

const HOMOGLYPH_MAP: Record<string, string> = {
  // Cyrillic → Latin equivalent
  "а": "a",
  "с": "c",
  "е": "e",
  "ё": "e",
  "н": "h",
  "і": "i",
  "к": "k",
  "м": "m",
  "о": "o",
  "р": "p",
  "ѕ": "s",
  "т": "t",
  "у": "y",
  "х": "x",
  // Latin → Cyrillic equivalents (normalize to lowercase latin)
  // These are already lowercase latin, but listed for doc clarity
}

const WORD_CHAR_CLASS = "A-Za-z0-9_\\u0400-\\u04FF"
const PROFANITY_PATTERNS = PROFANITY_WORDS.map(buildProfanityPattern)

// ─── Normalization pipeline ──────────────────────────────────────────────────

function normalizeLeet(input: string): string {
  let result = ""
  for (const ch of input) {
    result += LEET_MAP[ch] ?? ch
  }
  return result
}

function normalizeHomoglyphs(input: string): string {
  let result = ""
  for (const ch of input) {
    result += HOMOGLYPH_MAP[ch] ?? ch
  }
  return result
}

function normalize(text: string): string {
  const lower = text.toLowerCase()
  const leetNorm = normalizeLeet(lower)
  return normalizeHomoglyphs(leetNorm)
}

function escapeRegExp(value: string): string {
  return value.replace(/[.*+?^${}()|[\]\\]/g, "\\$&")
}

function buildProfanityPattern(word: string): RegExp {
  const separatedWord = [...word].map(escapeRegExp).join("[.\\-_\\s]*")
  return new RegExp(`(^|[^${WORD_CHAR_CLASS}])${separatedWord}($|[^${WORD_CHAR_CLASS}])`, "iu")
}

// ─── Public API ──────────────────────────────────────────────────────────────

/**
 * Check if a string contains profanity.
 * Applies leetspeak normalization, homoglyph folding, and separator-tolerant matching.
 * @returns true if profanity detected
 */
export function containsProfanity(text: string): boolean {
  if (!text) return false

  const normalized = normalize(text)
  const rawLower = text.toLowerCase()

  return PROFANITY_PATTERNS.some((pattern) => pattern.test(normalized) || pattern.test(rawLower))
}
