import { getApiBase } from '$lib/api/apiHelpers';

export function getHubUrl(path: string): string {
	const apiBase = getApiBase();
	return apiBase ? `${apiBase}${path}` : path;
}

export function getHubUrlCandidates(path: string): string[] {
	const primary = getHubUrl(path);
	if (!primary.endsWith(path)) {
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
