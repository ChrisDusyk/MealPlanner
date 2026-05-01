export { ApiError } from './apiHelpers';
import { ApiError, getApiBase, parseErrorBody } from './apiHelpers';
import type {
	CatalogueIngredient,
	CatalogueRecipe,
	CatalogueTag
} from './catalogueApi';
import type { ImportIngredientsResponse } from './recipeApi';

export interface AdminListCatalogueOptions {
	search?: string;
	isPublished?: boolean | null;
}

export interface CreateCatalogueRecipeRequest {
	name: string;
	description: string;
	servings: number;
	sourceUrl?: string | null;
	imageUrl?: string | null;
	ingredients: CatalogueIngredient[];
	tagIds: string[];
	isPublished: boolean;
}

export interface UpdateCatalogueRecipeRequest {
	name: string;
	description: string;
	servings: number;
	sourceUrl?: string | null;
	imageUrl?: string | null;
	ingredients: CatalogueIngredient[];
	tagIds: string[];
}

export interface SetCatalogueRecipePublishedRequest {
	isPublished: boolean;
}

export interface CatalogueTagRequest {
	name: string;
	slug: string;
}

function buildAdminListUrl(base: string, options: AdminListCatalogueOptions = {}): string {
	const params = new URLSearchParams();
	if (options.search) params.set('search', options.search);
	if (typeof options.isPublished === 'boolean')
		params.set('isPublished', String(options.isPublished));
	const query = params.toString();
	return `${base}/api/admin/catalogue${query ? `?${query}` : ''}`;
}

async function jsonRequest<T>(
	url: string,
	accessToken: string,
	method: string,
	body: unknown,
	fetchFn: typeof fetch
): Promise<T> {
	const response = await fetchFn(url, {
		method,
		headers: {
			'Content-Type': 'application/json',
			Authorization: `Bearer ${accessToken}`
		},
		body: body === undefined ? undefined : JSON.stringify(body)
	});

	if (!response.ok) {
		const { message, body: errBody } = await parseErrorBody(response);
		throw new ApiError(response.status, message, errBody);
	}
	if (response.status === 204) return undefined as T;
	return response.json();
}

export async function adminListCatalogue(
	accessToken: string,
	options: AdminListCatalogueOptions = {},
	fetchFn: typeof fetch = fetch
): Promise<CatalogueRecipe[]> {
	const response = await fetchFn(buildAdminListUrl(getApiBase(), options), {
		headers: { Authorization: `Bearer ${accessToken}` }
	});
	if (!response.ok) {
		const { message, body } = await parseErrorBody(response);
		throw new ApiError(response.status, message, body);
	}
	return response.json();
}

export async function adminGetCatalogueRecipe(
	accessToken: string,
	id: string,
	fetchFn: typeof fetch = fetch
): Promise<CatalogueRecipe> {
	const response = await fetchFn(
		`${getApiBase()}/api/admin/catalogue/${encodeURIComponent(id)}`,
		{ headers: { Authorization: `Bearer ${accessToken}` } }
	);
	if (!response.ok) {
		const { message, body } = await parseErrorBody(response);
		throw new ApiError(response.status, message, body);
	}
	return response.json();
}

export function adminCreateCatalogueRecipe(
	accessToken: string,
	request: CreateCatalogueRecipeRequest,
	fetchFn: typeof fetch = fetch
): Promise<CatalogueRecipe> {
	return jsonRequest<CatalogueRecipe>(
		`${getApiBase()}/api/admin/catalogue/`,
		accessToken,
		'POST',
		request,
		fetchFn
	);
}

export function adminUpdateCatalogueRecipe(
	accessToken: string,
	id: string,
	request: UpdateCatalogueRecipeRequest,
	fetchFn: typeof fetch = fetch
): Promise<CatalogueRecipe> {
	return jsonRequest<CatalogueRecipe>(
		`${getApiBase()}/api/admin/catalogue/${encodeURIComponent(id)}`,
		accessToken,
		'PUT',
		request,
		fetchFn
	);
}

export function adminSetCatalogueRecipePublished(
	accessToken: string,
	id: string,
	isPublished: boolean,
	fetchFn: typeof fetch = fetch
): Promise<CatalogueRecipe> {
	return jsonRequest<CatalogueRecipe>(
		`${getApiBase()}/api/admin/catalogue/${encodeURIComponent(id)}/published`,
		accessToken,
		'PUT',
		{ isPublished } satisfies SetCatalogueRecipePublishedRequest,
		fetchFn
	);
}

export function adminDeleteCatalogueRecipe(
	accessToken: string,
	id: string,
	fetchFn: typeof fetch = fetch
): Promise<void> {
	return jsonRequest<void>(
		`${getApiBase()}/api/admin/catalogue/${encodeURIComponent(id)}`,
		accessToken,
		'DELETE',
		undefined,
		fetchFn
	);
}

export function adminImportCatalogueFromUrl(
	accessToken: string,
	sourceUrl: string,
	fetchFn: typeof fetch = fetch
): Promise<ImportIngredientsResponse> {
	return jsonRequest<ImportIngredientsResponse>(
		`${getApiBase()}/api/admin/catalogue/import-from-url`,
		accessToken,
		'POST',
		{ sourceUrl },
		fetchFn
	);
}

export async function adminListCatalogueTags(
	accessToken: string,
	fetchFn: typeof fetch = fetch
): Promise<CatalogueTag[]> {
	const response = await fetchFn(`${getApiBase()}/api/admin/catalogue/tags/`, {
		headers: { Authorization: `Bearer ${accessToken}` }
	});
	if (!response.ok) {
		const { message, body } = await parseErrorBody(response);
		throw new ApiError(response.status, message, body);
	}
	return response.json();
}

export function adminCreateCatalogueTag(
	accessToken: string,
	request: CatalogueTagRequest,
	fetchFn: typeof fetch = fetch
): Promise<CatalogueTag> {
	return jsonRequest<CatalogueTag>(
		`${getApiBase()}/api/admin/catalogue/tags/`,
		accessToken,
		'POST',
		request,
		fetchFn
	);
}

export function adminUpdateCatalogueTag(
	accessToken: string,
	id: string,
	request: CatalogueTagRequest,
	fetchFn: typeof fetch = fetch
): Promise<CatalogueTag> {
	return jsonRequest<CatalogueTag>(
		`${getApiBase()}/api/admin/catalogue/tags/${encodeURIComponent(id)}`,
		accessToken,
		'PUT',
		request,
		fetchFn
	);
}

export function adminDeleteCatalogueTag(
	accessToken: string,
	id: string,
	fetchFn: typeof fetch = fetch
): Promise<void> {
	return jsonRequest<void>(
		`${getApiBase()}/api/admin/catalogue/tags/${encodeURIComponent(id)}`,
		accessToken,
		'DELETE',
		undefined,
		fetchFn
	);
}
