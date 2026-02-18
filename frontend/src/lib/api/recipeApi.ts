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

/**
 * Resolve the API base URL. On the server side we use the Aspire service
 * discovery env vars so requests go directly to the API rather than
 * through the Vite dev-server proxy (which only handles browser requests).
 */
function getApiBase(): string {
	if (typeof process !== 'undefined') {
		// Prefer HTTP in dev to avoid self-signed cert issues with Node fetch
		const url = process.env.services__api__http__0 || process.env.services__api__https__0;
		if (url) return url;
	}
	// Fallback for client-side / browser requests — use relative URL
	// which the Vite proxy will forward during dev.
	return '';
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
		throw new Error(`Failed to fetch recipes: ${response.status}`);
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
		const message = await response.text();
		throw new Error(message || `Failed to create recipe: ${response.status}`);
	}

	return response.json();
}
