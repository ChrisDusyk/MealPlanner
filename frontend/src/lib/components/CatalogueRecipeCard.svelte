<script lang="ts">
	import type { CatalogueRecipeListItem } from '$lib/api/catalogueApi';
	import { sanitizeUrl } from '$lib/utils/url';

	let {
		recipe,
		busy = false,
		onView,
		onAdd
	}: {
		recipe: CatalogueRecipeListItem;
		busy?: boolean;
		onView?: (recipe: CatalogueRecipeListItem) => void;
		onAdd?: (recipe: CatalogueRecipeListItem) => void;
	} = $props();

	const safeImage = $derived(sanitizeUrl(recipe.imageUrl));
</script>

<article
	class="group flex flex-col overflow-hidden rounded-xl border border-green-200/60 bg-white shadow-sm transition-shadow hover:shadow-md"
>
	{#if safeImage}
		<button
			type="button"
			class="block aspect-[16/10] w-full overflow-hidden bg-green-50 text-left"
			onclick={() => onView?.(recipe)}
			aria-label="View {recipe.name}"
		>
			<img
				src={safeImage}
				alt=""
				class="h-full w-full object-cover transition-transform group-hover:scale-105"
				loading="lazy"
			/>
		</button>
	{:else}
		<button
			type="button"
			class="flex aspect-[16/10] w-full items-center justify-center bg-gradient-to-br from-green-100 to-amber-100 text-green-700"
			onclick={() => onView?.(recipe)}
			aria-label="View {recipe.name}"
		>
			<svg
				xmlns="http://www.w3.org/2000/svg"
				class="h-10 w-10"
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
		</button>
	{/if}

	<div class="flex flex-1 flex-col gap-3 p-4">
		<div>
			<button
				type="button"
				class="text-left font-display text-base font-semibold leading-snug text-charcoal hover:text-green-700"
				onclick={() => onView?.(recipe)}
			>
				{recipe.name}
			</button>
			{#if recipe.description}
				<p class="mt-1 line-clamp-2 text-sm text-charcoal/60">{recipe.description}</p>
			{/if}
		</div>

		{#if recipe.tags.length > 0}
			<div class="flex flex-wrap gap-1.5">
				{#each recipe.tags as tag (tag.id)}
					<span
						class="inline-flex items-center rounded-full bg-green-50 px-2 py-0.5 text-xs font-medium text-green-700"
					>
						{tag.name}
					</span>
				{/each}
			</div>
		{/if}

		<div class="flex items-center justify-between text-xs text-charcoal/50">
			<span>{recipe.ingredientCount} ingredient{recipe.ingredientCount === 1 ? '' : 's'}</span>
			<span>{recipe.servings} serving{recipe.servings === 1 ? '' : 's'}</span>
		</div>

		<div class="mt-auto pt-1">
			{#if recipe.alreadyAdded}
				<span
					class="inline-flex w-full items-center justify-center gap-1.5 rounded-lg border border-green-200 bg-green-50 px-3 py-2 text-sm font-semibold text-green-700"
				>
					<svg
						xmlns="http://www.w3.org/2000/svg"
						class="h-4 w-4"
						fill="none"
						viewBox="0 0 24 24"
						stroke="currentColor"
						stroke-width="2.5"
					>
						<path stroke-linecap="round" stroke-linejoin="round" d="m4.5 12.75 6 6 9-13.5" />
					</svg>
					Already in your recipes
				</span>
			{:else}
				<button
					type="button"
					class="w-full rounded-lg bg-green-600 px-3 py-2 text-sm font-semibold text-white shadow-sm transition-colors hover:bg-green-700 disabled:cursor-not-allowed disabled:opacity-60"
					disabled={busy}
					onclick={() => onAdd?.(recipe)}
				>
					{busy ? 'Adding…' : 'Add to my recipes'}
				</button>
			{/if}
		</div>
	</div>
</article>
