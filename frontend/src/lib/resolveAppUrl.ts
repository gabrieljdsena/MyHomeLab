export function resolveAppUrl(url: string, hostname: string): string {
  try {
    const parsed = new URL(url)
    if (parsed.hostname === hostname) return url
    parsed.hostname = hostname
    return parsed.toString()
  } catch {
    return url
  }
}
