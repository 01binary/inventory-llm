export function stripThinkTags(text) {
  if (!text) {
    return "";
  }

  return text
    .replace(/<think\b[^>]*>[\s\S]*?<\/think>/gi, " ")
    .replace(/<think\b[^>]*>[\s\S]*$/gi, " ")
    .replace(/<\/think>/gi, " ")
    .replace(/\n{3,}/g, "\n\n")
    .trim();
}

export function stripMarkdownForSpeech(markdownText) {
  return markdownText
    .replace(/```[\s\S]*?```/g, " ")
    .replace(/`([^`]+)`/g, "$1")
    .replace(/!\[([^\]]*)\]\([^)]+\)/g, "$1")
    .replace(/\[([^\]]+)\]\([^)]+\)/g, "$1")
    .replace(/^\s{0,3}#{1,6}\s+/gm, "")
    .replace(/^\s*>\s?/gm, "")
    .replace(/^\s*[-*+]\s+/gm, "")
    .replace(/^\s*\d+\.\s+/gm, "")
    .replace(/[*_~#]/g, "")
    .replace(/\|/g, " ")
    .replace(/\s+/g, " ")
    .trim();
}
