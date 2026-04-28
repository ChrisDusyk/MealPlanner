import { getPublicApiBase } from '$lib/api/apiHelpers';

export function getHubUrl(path: string): string {
	const apiBase = getPublicApiBase();
	return apiBase ? `${apiBase}${path}` : path;
}

function isApiPrefixFallbackEnabled(): boolean {
	if (typeof process !== 'undefined') {
		const value = process.env.SIGNALR_USE_API_PREFIX_FALLBACK;
		if (value === '1' || value?.toLowerCase() === 'true') {
			return true;
		}
	}

	const viteValue = (import.meta.env.VITE_SIGNALR_USE_API_PREFIX_FALLBACK as string | undefined)
		?.trim()
		.toLowerCase();

	return viteValue === '1' || viteValue === 'true';
}

export function getHubUrlCandidates(path: string): string[] {
	const primary = getHubUrl(path);
	if (!primary.endsWith(path) || !isApiPrefixFallbackEnabled()) {
		return [primary];
	}

	const base = primary.slice(0, -path.length).replace(/\/$/, '');
	const alternate = `${base}/api${path}`;

	return alternate === primary ? [primary] : [primary, alternate];
}

export function isSignalRHubNotFoundError(error: unknown): boolean {
	if (!(error instanceof Error)) {
		return false;
	}

	return /status code '404'|not a signalr endpoint|proxy blocking the connection/i.test(
		error.message
	);
}
