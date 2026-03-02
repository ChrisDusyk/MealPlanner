export { ApiError } from './apiHelpers';
import { ApiError, getApiBase, parseErrorBody } from './apiHelpers';

export interface Ingredient {
	name: string;
	quantity: number;
	unit: string;
	isPantryStaple: boolean;
}

export interface Recipe {
	id: string;
	name: string;
	description: string;
	servings: number;
	sourceUrl?: string | null;
	ingredients: Ingredient[];
	createdAt: string;
	updatedAt: string;
}

export interface CreateRecipeRequest {
	name: string;
	description: string;
	servings?: number;
	sourceUrl?: string | null;
	ingredients: Ingredient[];
}

export interface UpdateRecipeRequest {
	name: string;
	description: string;
	servings?: number;
	sourceUrl?: string | null;
	ingredients: Ingredient[];
}

export interface ImportIngredientsRequest {
	sourceUrl: string;
}

export interface ImportIngredientsResponse {
	ingredients: Ingredient[];
	warnings: string[];
}

export async function fetchRecipes(
	accessToken: string,
	fetchFn: typeof fetch = fetch
): Promise<Recipe[]> {
	const response = await fetchFn(`${getApiBase()}/api/recipes`, {
		headers: {
			Authorization: `Bearer ${accessToken}`
		}
	});

	if (!response.ok) {
		const { message, body } = await parseErrorBody(response);
		throw new ApiError(response.status, message, body);
	}

	return response.json();
}

export async function importRecipeIngredients(
	accessToken: string,
	request: ImportIngredientsRequest,
	fetchFn: typeof fetch = fetch
): Promise<ImportIngredientsResponse> {
	const response = await fetchFn(`${getApiBase()}/api/recipes/import-ingredients`, {
		method: 'POST',
		headers: {
			'Content-Type': 'application/json',
			Authorization: `Bearer ${accessToken}`
		},
		body: JSON.stringify(request)
	});

	if (!response.ok) {
		const { message, body } = await parseErrorBody(response);
		throw new ApiError(response.status, message, body);
	}

	return response.json();
}

export async function createRecipe(
	accessToken: string,
	request: CreateRecipeRequest,
	fetchFn: typeof fetch = fetch
): Promise<Recipe> {
	const response = await fetchFn(`${getApiBase()}/api/recipes`, {
		method: 'POST',
		headers: {
			'Content-Type': 'application/json',
			Authorization: `Bearer ${accessToken}`
		},
		body: JSON.stringify(request)
	});

	if (!response.ok) {
		const { message, body } = await parseErrorBody(response);
		throw new ApiError(response.status, message, body);
	}

	return response.json();
}

export async function fetchRecipeById(
	accessToken: string,
	id: string,
	fetchFn: typeof fetch = fetch
): Promise<Recipe> {
	const response = await fetchFn(`${getApiBase()}/api/recipes/${encodeURIComponent(id)}`, {
		headers: {
			Authorization: `Bearer ${accessToken}`
		}
	});

	if (!response.ok) {
		const { message, body } = await parseErrorBody(response);
		throw new ApiError(response.status, message, body);
	}

	return response.json();
}

export async function updateRecipe(
	accessToken: string,
	id: string,
	request: UpdateRecipeRequest,
	fetchFn: typeof fetch = fetch
): Promise<Recipe> {
	const response = await fetchFn(`${getApiBase()}/api/recipes/${encodeURIComponent(id)}`, {
		method: 'PUT',
		headers: {
			'Content-Type': 'application/json',
			Authorization: `Bearer ${accessToken}`
		},
		body: JSON.stringify(request)
	});

	if (!response.ok) {
		const { message, body } = await parseErrorBody(response);
		throw new ApiError(response.status, message, body);
	}

	return response.json();
}
