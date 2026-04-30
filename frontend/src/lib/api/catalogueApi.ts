export { ApiError } from './apiHelpers';
import { ApiError, getApiBase, parseErrorBody } from './apiHelpers';
import type { Ingredient } from './recipeApi';

export interface CatalogueTag {
	id: string;
	slug: string;
	name: string;
}

export interface CatalogueIngredient extends Ingredient {}

export interface CatalogueRecipeListItem {
	id: string;
	name: string;
	description: string;
	imageUrl?: string | null;
	servings: number;
	ingredientCount: number;
	addCount: number;
	isPublished: boolean;
	publishedAt?: string | null;
	tags: CatalogueTag[];
	alreadyAdded: boolean;
	updatedAt: string;
}

export interface CatalogueRecipe {
	id: string;
	name: string;
	description: string;
	servings: number;
	sourceUrl?: string | null;
	imageUrl?: string | null;
	ingredients: CatalogueIngredient[];
	isPublished: boolean;
	publishedAt?: string | null;
	addCount: number;
	tags: CatalogueTag[];
	alreadyAdded: boolean;
	createdAt: string;
	updatedAt: string;
}

export type CatalogueSort = 'recent' | 'popular';

export interface BrowseCatalogueOptions {
	search?: string;
	tagSlugs?: string[];
	sort?: CatalogueSort;
	skip?: number;
	take?: number;
}

export interface AddCatalogueRecipeResponse {
	recipeId: string;
}

function buildBrowseUrl(base: string, options: BrowseCatalogueOptions = {}): string {
	const params = new URLSearchParams();
	if (options.search) params.set('search', options.search);
	if (options.tagSlugs && options.tagSlugs.length > 0)
		params.set('tags', options.tagSlugs.join(','));
	if (options.sort) params.set('sort', options.sort);
	if (typeof options.skip === 'number') params.set('skip', String(options.skip));
	if (typeof options.take === 'number') params.set('take', String(options.take));
	const query = params.toString();
	return `${base}/api/catalogue${query ? `?${query}` : ''}`;
}

export async function fetchCatalogue(
	accessToken: string,
	options: BrowseCatalogueOptions = {},
	fetchFn: typeof fetch = fetch
): Promise<CatalogueRecipeListItem[]> {
	const response = await fetchFn(buildBrowseUrl(getApiBase(), options), {
		headers: { Authorization: `Bearer ${accessToken}` }
	});

	if (!response.ok) {
		const { message, body } = await parseErrorBody(response);
		throw new ApiError(response.status, message, body);
	}
	return response.json();
}

export async function fetchCatalogueRecipe(
	accessToken: string,
	id: string,
	fetchFn: typeof fetch = fetch
): Promise<CatalogueRecipe> {
	const response = await fetchFn(`${getApiBase()}/api/catalogue/${encodeURIComponent(id)}`, {
		headers: { Authorization: `Bearer ${accessToken}` }
	});

	if (!response.ok) {
		const { message, body } = await parseErrorBody(response);
		throw new ApiError(response.status, message, body);
	}
	return response.json();
}

export async function fetchCatalogueTags(
	accessToken: string,
	fetchFn: typeof fetch = fetch
): Promise<CatalogueTag[]> {
	const response = await fetchFn(`${getApiBase()}/api/catalogue/tags`, {
		headers: { Authorization: `Bearer ${accessToken}` }
	});

	if (!response.ok) {
		const { message, body } = await parseErrorBody(response);
		throw new ApiError(response.status, message, body);
	}
	return response.json();
}

export async function addCatalogueRecipeToMyRecipes(
	accessToken: string,
	id: string,
	fetchFn: typeof fetch = fetch
): Promise<AddCatalogueRecipeResponse> {
	const response = await fetchFn(
		`${getApiBase()}/api/catalogue/${encodeURIComponent(id)}/add-to-my-recipes`,
		{
			method: 'POST',
			headers: { Authorization: `Bearer ${accessToken}` }
		}
	);

	if (!response.ok) {
		const { message, body } = await parseErrorBody(response);
		throw new ApiError(response.status, message, body);
	}
	return response.json();
}
