/**
 * Validates that a URL uses only safe schemes (http or https).
 * Returns the URL if safe, or null if it should be rejected.
 */
export function sanitizeUrl(url: string | null | undefined): string | null {
	if (!url) return null;
	try {
		const parsed = new URL(url);
		if (parsed.protocol === 'http:' || parsed.protocol === 'https:') {
			return url;
		}
		return null;
	} catch {
		return null;
	}
}
