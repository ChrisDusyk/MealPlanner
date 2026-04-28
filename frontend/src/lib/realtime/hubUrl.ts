import { getApiBase } from '$lib/api/apiHelpers';

export function getHubUrl(path: string): string {
	const apiBase = getApiBase();
	return apiBase ? `${apiBase}${path}` : path;
}
