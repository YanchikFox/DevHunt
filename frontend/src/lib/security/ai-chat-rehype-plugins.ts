import type { PluggableList } from "unified";
import rehypeRaw from "rehype-raw";
import rehypeSanitize from "rehype-sanitize";

/**
 * Rehype plugins for AI assistant markdown: parse inline HTML, then strip XSS vectors.
 */
export const aiChatRehypePlugins: PluggableList = [rehypeRaw, rehypeSanitize];
