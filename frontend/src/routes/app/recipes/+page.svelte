<script lang="ts">
	import RecipeCard from '$lib/components/RecipeCard.svelte';

	let { data } = $props();
</script>

<svelte:head>
	<title>My Recipes — MealPlanner</title>
</svelte:head>

<div class="mx-auto max-w-6xl">
	<!-- Page header -->
	<div class="mb-8">
		<h1 class="font-display text-3xl font-bold text-charcoal">My Recipes</h1>
		<p class="mt-1 text-charcoal/60">
			Manage your collection of recipes and discover new meal ideas.
		</p>
	</div>

	{#if data.recipes.length === 0}
		<!-- Empty state -->
		<div
			class="flex flex-col items-center justify-center rounded-2xl border-2 border-dashed border-green-300/40 bg-green-50/50 px-6 py-16"
		>
			<div
				class="mb-4 flex h-16 w-16 items-center justify-center rounded-2xl bg-green-100 text-green-600"
			>
				<svg
					xmlns="http://www.w3.org/2000/svg"
					class="h-8 w-8"
					fill="none"
					viewBox="0 0 24 24"
					stroke="currentColor"
					stroke-width="1.5"
				>
					<path
						stroke-linecap="round"
						stroke-linejoin="round"
						d="M12 6.042A8.967 8.967 0 0 0 6 3.75c-1.052 0-2.062.18-3 .512v14.25A8.987 8.987 0 0 1 6 18c2.305 0 4.408.867 6 2.292m0-14.25a8.966 8.966 0 0 1 6-2.292c1.052 0 2.062.18 3 .512v14.25A8.987 8.987 0 0 0 18 18a8.967 8.967 0 0 0-6 2.292m0-14.25v14.25"
					/>
				</svg>
			</div>
			<h2 class="font-display text-lg font-semibold text-charcoal">No recipes yet</h2>
			<p class="mt-1 max-w-sm text-center text-sm text-charcoal/50">
				You haven't added any recipes to your collection. Start building your meal plan by adding
				your first recipe!
			</p>
		</div>
	{:else}
		<!-- Recipe grid -->
		<div class="grid gap-5 sm:grid-cols-2 lg:grid-cols-3">
			{#each data.recipes as recipe (recipe.id)}
				<RecipeCard {recipe} />
			{/each}
		</div>
	{/if}
</div>
