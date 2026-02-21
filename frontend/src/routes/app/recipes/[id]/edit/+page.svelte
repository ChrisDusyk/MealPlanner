<script lang="ts">
	import { goto } from '$app/navigation';
	import RecipeForm from '$lib/components/RecipeForm.svelte';
	import type { CreateRecipeRequest } from '$lib/api/recipeApi';

	let { data } = $props();

	const recipe = $derived(data.recipe);

	let submitting = $state(false);
	let errorMessage = $state('');

	const initialData = $derived({
		name: recipe.name,
		description: recipe.description,
		sourceUrl: recipe.sourceUrl ?? '',
		ingredients: recipe.ingredients
	});

	async function handleSubmit(request: CreateRecipeRequest) {
		submitting = true;
		errorMessage = '';

		try {
			const response = await fetch(`/app/recipes/${recipe.id}/edit`, {
				method: 'PUT',
				headers: { 'Content-Type': 'application/json' },
				body: JSON.stringify(request)
			});

			if (!response.ok) {
				try {
					const contentType = response.headers.get('content-type') || '';
					if (contentType.includes('application/json')) {
						const result = await response.json();
						errorMessage = result.error || result.message || 'Failed to update recipe.';
					} else {
						errorMessage = (await response.text()) || 'Failed to update recipe.';
					}
				} catch {
					errorMessage = 'Failed to update recipe.';
				}
				return;
			}

			await goto(`/app/recipes/${recipe.id}`);
		} catch (err) {
			errorMessage = 'An unexpected error occurred. Please try again.';
		} finally {
			submitting = false;
		}
	}
</script>

<svelte:head>
	<title>Edit {recipe.name} — MealPlanner</title>
</svelte:head>

<div class="mx-auto max-w-3xl">
	<!-- Page header -->
	<div class="mb-8">
		<a
			href="/app/recipes/{recipe.id}"
			class="mb-4 inline-flex items-center gap-1.5 font-display text-sm font-medium text-green-600 transition-colors hover:text-green-700"
		>
			<svg
				xmlns="http://www.w3.org/2000/svg"
				class="h-4 w-4"
				fill="none"
				viewBox="0 0 24 24"
				stroke="currentColor"
				stroke-width="2"
			>
				<path stroke-linecap="round" stroke-linejoin="round" d="M15 19l-7-7 7-7" />
			</svg>
			Back to Recipe
		</a>
		<h1 class="font-display text-3xl font-bold text-charcoal">Edit Recipe</h1>
		<p class="mt-1 text-charcoal/60">Update the details for "{recipe.name}".</p>
	</div>

	<RecipeForm
		{initialData}
		submitLabel="Update Recipe"
		{submitting}
		{errorMessage}
		onsubmit={handleSubmit}
	/>
</div>
