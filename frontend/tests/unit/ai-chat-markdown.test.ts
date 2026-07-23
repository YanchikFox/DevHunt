import { describe, expect, it } from "vitest"
import { unified } from "unified"
import remarkParse from "remark-parse"
import remarkGfm from "remark-gfm"
import remarkRehype from "remark-rehype"
import rehypeStringify from "rehype-stringify"
import { aiChatRehypePlugins } from "@/lib/security/ai-chat-rehype-plugins"

/**
 * Mirrors AiChatWindow ReactMarkdown pipeline for XSS regression checks.
 */
async function renderAiChatMarkdown(source: string): Promise<string> {
  const processor = unified()
    .use(remarkParse)
    .use(remarkGfm)
    .use(remarkRehype, { allowDangerousHtml: true })
    .use(aiChatRehypePlugins)
    .use(rehypeStringify)

  const file = await processor.process(source)
  return String(file)
}

describe("aiChatRehypePlugins", () => {
  it("strips script tags from model HTML", async () => {
    const html = await renderAiChatMarkdown('<script>alert("xss")</script>\n\nHello')
    expect(html).not.toContain("<script")
    expect(html).toContain("Hello")
  })

  it("removes inline event handlers from img tags", async () => {
    const html = await renderAiChatMarkdown('<img src="x" onerror="alert(1)" alt="x" />')
    expect(html).not.toMatch(/onerror/i)
    expect(html).not.toContain("alert(1)")
  })

  it("preserves safe markdown formatting", async () => {
    const html = await renderAiChatMarkdown("**bold** and `code`")
    expect(html).toContain("<strong>bold</strong>")
    expect(html).toContain("<code>code</code>")
  })
})
