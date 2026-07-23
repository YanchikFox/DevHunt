import { type ClassValue, clsx } from "clsx"
import { twMerge } from "tailwind-merge"

/**
 * Utility function to merge Tailwind CSS classes.
 * Combines `clsx` for conditional classes and `tailwind-merge` to handle conflicts.
 *
 * @param inputs - Class names or conditional class objects.
 * @returns Merged class string.
 */
export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs))
}

/**
 * Recursively converts object keys from PascalCase to camelCase.
 * Useful when transforming API responses (often PascalCase in .NET) to JavaScript conventions.
 *
 * @param obj - The object, array, or value to convert.
 * @returns The converted object with camelCase keys.
 */
export function toCamelCase(obj: unknown): unknown {
  if (obj === null || obj === undefined) {
    return obj
  }
  if (Array.isArray(obj)) {
    return obj.map((v) => toCamelCase(v))
  } else if (obj !== null && typeof obj === 'object' && obj.constructor === Object) {
    return Object.keys(obj).reduce((result, key) => {
      // Simple PascalCase to camelCase conversion
      // Handles: "Id" -> "id", "ConversationId" -> "conversationId"
      const camelKey = key.charAt(0).toLowerCase() + key.slice(1)
      result[camelKey] = toCamelCase((obj as Record<string, unknown>)[key])
      return result
    }, {} as Record<string, unknown>)
  }
  return obj
}

/**
 * Recursively converts object keys from camelCase to PascalCase.
 * Useful when preparing data to send to a .NET backend.
 *
 * @param obj - The object, array, or value to convert.
 * @returns The converted object with PascalCase keys.
 */
export function toPascalCase(obj: unknown): unknown {
  if (obj === null || obj === undefined) {
    return obj
  }
  if (Array.isArray(obj)) {
    return obj.map((v) => toPascalCase(v))
  } else if (obj !== null && typeof obj === 'object' && obj.constructor === Object) {
    return Object.keys(obj).reduce((result, key) => {
      // Simple camelCase to PascalCase conversion
      // Handles: "id" -> "Id", "conversationId" -> "ConversationId"
      const pascalKey = key.charAt(0).toUpperCase() + key.slice(1)
      result[pascalKey] = toPascalCase((obj as Record<string, unknown>)[key])
      return result
    }, {} as Record<string, unknown>)
  }
  return obj
}
