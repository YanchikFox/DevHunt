/**
 * Password validation utilities - must match backend validation rules
 * Backend: DevHunt.AuthService/Services/AuthValidationService.cs
 */
import { z } from "zod";

/** Allowed special characters - must match backend SpecialCharacters constant */
export const SPECIAL_CHARACTERS = "@$!%*?&";

/** Minimum password length - must match backend MinPasswordLength */
export const MIN_PASSWORD_LENGTH = 8;

/** Maximum password length - must match backend MaxPasswordLength */
export const MAX_PASSWORD_LENGTH = 128;

/** Regex pattern for special characters - escaped for use in regex */
export const SPECIAL_CHAR_REGEX = /[@$!%*?&]/;

/** Full password complexity regex matching backend requirements */
export const PASSWORD_COMPLEXITY_REGEX = /^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&]).+$/;

export interface PasswordRequirement {
    name: string;
    label: string;
    test: (password: string) => boolean;
}

/**
 * Get password requirements with localized labels
 * @param t - Translation function
 * @returns Array of password requirements
 */
export function getPasswordRequirements(t: (key: string, defaultValue?: { defaultValue: string }) => string): PasswordRequirement[] {
    return [
        {
            name: "minLength",
            label: t("auth.passwordReqMinLength", { defaultValue: `At least ${MIN_PASSWORD_LENGTH} characters` }),
            test: (p: string) => p.length >= MIN_PASSWORD_LENGTH,
        },
        {
            name: "maxLength",
            label: t("auth.passwordReqMaxLength", { defaultValue: `At most ${MAX_PASSWORD_LENGTH} characters` }),
            test: (p: string) => p.length <= MAX_PASSWORD_LENGTH,
        },
        {
            name: "uppercase",
            label: t("auth.passwordReqUppercase", { defaultValue: "One uppercase letter" }),
            test: (p: string) => /[A-Z]/.test(p),
        },
        {
            name: "lowercase",
            label: t("auth.passwordReqLowercase", { defaultValue: "One lowercase letter" }),
            test: (p: string) => /[a-z]/.test(p),
        },
        {
            name: "digit",
            label: t("auth.passwordReqDigit", { defaultValue: "One number" }),
            test: (p: string) => /\d/.test(p),
        },
        {
            name: "specialChar",
            label: t("auth.passwordReqSpecialChar", { defaultValue: `One special character (${SPECIAL_CHARACTERS})` }),
            test: (p: string) => SPECIAL_CHAR_REGEX.test(p),
        },
    ];
}

/**
 * Validate password against all requirements
 * @param password - Password to validate
 * @returns True if password meets all requirements
 */
export function isValidPassword(password: string): boolean {
    if (!password) return false;

    return (
        password.length >= MIN_PASSWORD_LENGTH &&
        password.length <= MAX_PASSWORD_LENGTH &&
        /[A-Z]/.test(password) &&
        /[a-z]/.test(password) &&
        /\d/.test(password) &&
        SPECIAL_CHAR_REGEX.test(password)
    );
}

/**
 * Create Zod password schema for forms
 * @param t - Translation function
 * @returns Zod string schema with password validation
 */
export function createPasswordZodSchema(t: (key: string) => string) {
    return z
        .string()
        .min(MIN_PASSWORD_LENGTH, t("auth.passwordMinLength8"))
        .max(MAX_PASSWORD_LENGTH, t("auth.passwordMaxLength"))
        .regex(
            PASSWORD_COMPLEXITY_REGEX,
            t("auth.passwordComplexity")
        );
}
