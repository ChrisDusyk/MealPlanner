export interface Ingredient {
	name: string;
	quantity: number;
	unit: string;
}

export interface Recipe {
	id: string;
	name: string;
	description: string;
	sourceUrl: string;
	ingredients: Ingredient[];
	createdAt: string;
	updatedAt: string;
}

export interface CreateRecipeRequest {
	name: string;
	description: string;
	sourceUrl: string;
	ingredients: Ingredient[];
}

type FetchFn = typeof globalThis.fetch;

export async function fetchRecipes(
	accessToken: string,
	fetchFn: FetchFn = fetch
): Promise<Recipe[]> {
	const response = await fetchFn('/api/recipes', {
		headers: {
			Authorization: `Bearer ${accessToken}`
		}
	});

	if (!response.ok) {
		throw new Error(`Failed to fetch recipes: ${response.status}`);
	}

	return response.json();
}

export async function createRecipe(
	accessToken: string,
	request: CreateRecipeRequest,
	fetchFn: FetchFn = fetch
): Promise<Recipe> {
	const response = await fetchFn('/api/recipes', {
		method: 'POST',
		headers: {
			'Content-Type': 'application/json',
			Authorization: `Bearer ${accessToken}`
		},
		body: JSON.stringify(request)
	});

	if (!response.ok) {
		const message = await response.text();
		throw new Error(message || `Failed to create recipe: ${response.status}`);
	}

	return response.json();
}
