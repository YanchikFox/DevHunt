/**
 * Simple template renderer
 * Substitutes data into HTML templates
 */

import { logger } from "./logger.js";

// XSS-01: HTML entity encoding for all user-controlled data inserted into email HTML.
// Prevents stored/reflected XSS via userName, projectName, messageContent, etc.
function escapeHtml(value) {
  return String(value ?? "")
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;")
    .replace(/'/g, "&#x27;");
}

// XSS-02: Validate href attributes — only allow https:// and http:// URLs.
// Prevents javascript: and data: URI injection via projectUrl / chatUrl.
function safeHref(url, fallback = "#") {
  if (!url) return fallback;
  const str = String(url);
  if (/^https?:\/\//i.test(str)) return str;
  return fallback;
}

const templates = {
  welcome: (data) => `
        <html>
            <body style="font-family: Arial, sans-serif; padding: 20px;">
                <h2>Witaj w DevHunt, ${escapeHtml(data.userName || "użytkowniku")}!</h2>
                <p>Dziękujemy za rejestrację. Cieszymy się, że jesteś w naszej społeczności programistów!</p>
                <p>Zacznij tworzyć projekty, znajdź zespół i dziel się swoimi osiągnięciami.</p>
            </body>
        </html>
    `,

  projectInvitation: (data) => `
        <html>
            <body style="font-family: Arial, sans-serif; padding: 20px;">
                <h2>Zaproszenie do projektu: ${escapeHtml(data.projectName || "Projekt")}</h2>
                <p>Cześć, ${escapeHtml(data.userName || "użytkowniku")}!</p>
                <p>${escapeHtml(data.inviterName || "Ktoś")} zaprasza Cię do dołączenia do projektu &ldquo;${escapeHtml(data.projectName)}&rdquo;.</p>
                <p><a href="${safeHref(data.projectUrl)}" style="background: #007bff; color: white; padding: 10px 20px; text-decoration: none; border-radius: 5px;">Zobacz projekt</a></p>
            </body>
        </html>
    `,

  newMessage: (data) => `
        <html>
            <body style="font-family: Arial, sans-serif; padding: 20px;">
                <h2>Nowa wiadomość na czacie</h2>
                <p>${escapeHtml(data.senderName || "Ktoś")} wysłał Ci wiadomość:</p>
                <div style="background: #f5f5f5; padding: 15px; border-radius: 5px; margin: 15px 0;">
                    ${escapeHtml(data.messageContent || "")}
                </div>
                <p><a href="${safeHref(data.chatUrl)}">Otwórz czat</a></p>
            </body>
        </html>
    `,

  achievementUnlocked: (data) => `
        <html>
            <body style="font-family: Arial, sans-serif; padding: 20px;">
                <h2>Nowe osiągnięcie!</h2>
                <p>Gratulacje, ${escapeHtml(data.userName || "użytkowniku")}! Zdobyłeś osiągnięcie:</p>
                <h3>${escapeHtml(data.achievementName || "Osiągnięcie")}</h3>
                <p>${escapeHtml(data.achievementDescription || "")}</p>
                <p>Tak trzymaj!</p>
            </body>
        </html>
    `,
};

/**
 * Render template with data
 */
export function renderTemplate(templateName, data = {}) {
  try {
    const template = templates[templateName];

    if (!template) {
      logger.warn(`Template "${templateName}" not found, using default`);
      return `<html><body><p>Notification: ${escapeHtml(JSON.stringify(data))}</p></body></html>`;
    }

    if (typeof template === "function") {
      return template(data);
    }

    return template;
  } catch (error) {
    logger.error(`Error rendering template "${templateName}":`, error);
    return `<html><body><p>Error rendering template.</p></body></html>`;
  }
}
