<script lang="ts">
	import { formatDate } from '$lib/utils/date';
	import { sanitizeUrl } from '$lib/utils/url';

	let { data } = $props();

	const recipe = $derived(data.recipe);
	const safeSourceUrl = $derived(sanitizeUrl(recipe.sourceUrl));
</script>

<svelte:head>
	<title>{recipe.name} — Simple Meal Planner</title>
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

		<div class="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between sm:gap-4">
			<div>
				<h1 class="font-display text-2xl font-bold text-charcoal sm:text-3xl">{recipe.name}</h1>
				<div class="mt-2 flex flex-wrap items-center gap-x-4 gap-y-1 text-sm text-charcoal/50">
					<span>Created {formatDate(recipe.createdAt)}</span>
					<span>Updated {formatDate(recipe.updatedAt)}</span>
				</div>
			</div>
			<a
				href="/app/recipes/{recipe.id}/edit"
				class="flex w-fit shrink-0 items-center gap-2 rounded-lg bg-green-600 px-5 py-2.5 font-display text-sm font-semibold text-white shadow-md shadow-green-900/20 transition-all hover:bg-green-700 hover:shadow-lg"
			>
				<svg
					xmlns="http://www.w3.org/2000/svg"
					class="h-4 w-4"
					fill="none"
					viewBox="0 0 24 24"
					stroke="currentColor"
					stroke-width="2"
				>
					<path
						stroke-linecap="round"
						stroke-linejoin="round"
						d="m16.862 4.487 1.687-1.688a1.875 1.875 0 1 1 2.652 2.652L10.582 16.07a4.5 4.5 0 0 1-1.897 1.13L6 18l.8-2.685a4.5 4.5 0 0 1 1.13-1.897l8.932-8.931Zm0 0L19.5 7.125M18 14v4.75A2.25 2.25 0 0 1 15.75 21H5.25A2.25 2.25 0 0 1 3 18.75V8.25A2.25 2.25 0 0 1 5.25 6H10"
					/>
				</svg>
				Edit Recipe
			</a>
		</div>
	</div>

	<!-- Description -->
	{#if recipe.description}
		<section class="mb-6 overflow-hidden rounded-xl border border-green-200/50 bg-white shadow-sm">
			<div class="border-b border-green-100/60 bg-green-50/30 px-6 py-4">
				<h2 class="font-display text-lg font-semibold text-charcoal">Description</h2>
			</div>
			<div class="p-6">
				<p class="whitespace-pre-line text-sm leading-relaxed text-charcoal/70">{recipe.description}</p>
			</div>
		</section>
	{/if}

	<!-- Servings -->
	{#if recipe.servings > 1}
		<div class="mb-6 flex items-center gap-2 rounded-lg border border-green-200/50 bg-white px-5 py-3 shadow-sm">
			<span class="text-sm font-medium text-charcoal/70">Servings:</span>
			<span class="text-sm font-semibold text-charcoal">{recipe.servings}</span>
		</div>
	{/if}

	<!-- Source URL -->
	{#if safeSourceUrl}
		<section class="mb-6 overflow-hidden rounded-xl border border-green-200/50 bg-white shadow-sm">
			<div class="flex items-center gap-3 px-6 py-4">
				<svg
					xmlns="http://www.w3.org/2000/svg"
					class="h-5 w-5 text-green-500"
					fill="none"
					viewBox="0 0 24 24"
					stroke="currentColor"
					stroke-width="2"
				>
					<path
						stroke-linecap="round"
						stroke-linejoin="round"
						d="M13.5 6H5.25A2.25 2.25 0 0 0 3 8.25v10.5A2.25 2.25 0 0 0 5.25 21h10.5A2.25 2.25 0 0 0 18 18.75V10.5m-10.5 6L21 3m0 0h-5.25M21 3v5.25"
					/>
				</svg>
				<a
					href={safeSourceUrl}
					target="_blank"
					rel="noopener noreferrer"
					class="block truncate text-sm font-medium text-green-600 transition-colors hover:text-green-700 hover:underline"
				>
					{safeSourceUrl}
				</a>
			</div>
		</section>
	{/if}

	<!-- Ingredients -->
	<section class="overflow-hidden rounded-xl border border-green-200/50 bg-white shadow-sm">
		<div class="border-b border-green-100/60 bg-green-50/30 px-6 py-4">
			<h2 class="font-display text-lg font-semibold text-charcoal">Ingredients</h2>
			{#if recipe.ingredients.length > 0}
				<p class="mt-0.5 text-xs text-charcoal/40">
					{recipe.ingredients.length} ingredient{recipe.ingredients.length !== 1 ? 's' : ''}
				</p>
			{/if}
		</div>
		<div class="p-6">
			{#if recipe.ingredients.length === 0}
				<div class="flex flex-col items-center justify-center rounded-xl border-2 border-dashed border-green-200/40 py-12">
					<p class="font-display text-sm font-medium text-charcoal/50">No ingredients listed</p>
					<p class="mt-0.5 text-xs text-charcoal/30">This recipe doesn't have any ingredients yet.</p>
				</div>
			{:else}
				<ul class="divide-y divide-green-100/60">
					{#each recipe.ingredients as ingredient}
						<li class="flex items-center gap-3 py-3 first:pt-0 last:pb-0">
							<div class="flex h-8 w-8 shrink-0 items-center justify-center rounded-lg bg-green-100 text-green-600">
								<svg
									xmlns="http://www.w3.org/2000/svg"
									class="h-4 w-4"
									fill="none"
									viewBox="0 0 24 24"
									stroke="currentColor"
									stroke-width="2"
								>
									<path stroke-linecap="round" stroke-linejoin="round" d="m4.5 12.75 6 6 9-13.5" />
								</svg>
							</div>
							<div class="flex-1">
								<span class="text-sm font-medium text-charcoal">{ingredient.name}</span>
							</div>
							<span class="text-sm text-charcoal/60">
								{ingredient.quantity}{ingredient.unit ? ` ${ingredient.unit}` : ''}
							</span>
						</li>
					{/each}
				</ul>
			{/if}
		</div>
	</section>
</div>
