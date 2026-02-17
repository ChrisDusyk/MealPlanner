export interface Ingredient {
	name: string;
	quantity: number;
	unit: string;
}

export interface Recipe {
	id: string;
	userId: string;
	name: string;
	description: string;
	sourceUrl: string;
	ingredients: Ingredient[];
	createdAt: string;
	updatedAt: string;
}

export async function fetchRecipes(accessToken: string): Promise<Recipe[]> {
	const response = await fetch('/api/recipes', {
		headers: {
			Authorization: `Bearer ${accessToken}`
		}
	});

	if (!response.ok) {
		throw new Error(`Failed to fetch recipes: ${response.status}`);
	}

	return response.json();
}
