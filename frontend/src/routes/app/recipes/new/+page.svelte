<script lang="ts">
	import { goto } from '$app/navigation';
	import RecipeForm from '$lib/components/RecipeForm.svelte';
	import type { CreateRecipeRequest } from '$lib/api/recipeApi';

	let submitting = $state(false);
	let errorMessage = $state('');

	async function handleSubmit(request: CreateRecipeRequest) {
		submitting = true;
		errorMessage = '';

		try {
			const response = await fetch('/app/recipes/new', {
				method: 'POST',
				headers: { 'Content-Type': 'application/json' },
				body: JSON.stringify(request)
			});

			if (!response.ok) {
				try {
					const contentType = response.headers.get('content-type') || '';
					if (contentType.includes('application/json')) {
						const result = await response.json();
						errorMessage = result.error || result.message || 'Failed to create recipe.';
					} else {
						errorMessage = (await response.text()) || 'Failed to create recipe.';
					}
				} catch {
					errorMessage = 'Failed to create recipe.';
				}
				return;
			}

			await goto('/app/recipes');
		} catch (err) {
			errorMessage = 'An unexpected error occurred. Please try again.';
		} finally {
			submitting = false;
		}
	}
</script>

<svelte:head>
	<title>New Recipe — Simple Meal Planner</title>
</svelte:head>

<div class="mx-auto max-w-3xl">
	<!-- Page header -->
	<div class="mb-8">
		<a
			href="/app/recipes"
			class="mb-4 inline-flex items-center gap-1.5 py-2 font-display text-sm font-medium text-green-600 transition-colors hover:text-green-700"
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
			Back to Recipes
		</a>
		<h1 class="font-display text-2xl font-bold text-charcoal sm:text-3xl">New Recipe</h1>
		<p class="mt-1 text-charcoal/60">Add a new recipe to your collection.</p>
	</div>

	<RecipeForm
		submitLabel="Save Recipe"
		{submitting}
		{errorMessage}
		onsubmit={handleSubmit}
	/>
</div>
