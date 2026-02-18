<script lang="ts">
	import type { Recipe } from '$lib/api/recipeApi';
	import { formatDate } from '$lib/utils/date';
	import { sanitizeUrl } from '$lib/utils/url';

	let { recipe }: { recipe: Recipe } = $props();

	const safeSourceUrl = $derived(sanitizeUrl(recipe.sourceUrl));
</script>

<article
	class="group relative overflow-hidden rounded-xl border border-green-200/50 bg-white shadow-sm transition-all duration-200 hover:-translate-y-0.5 hover:shadow-md hover:shadow-green-900/5"
>
	<div class="p-5">
		<!-- Title -->
		<h3
			class="font-display text-lg font-semibold leading-tight text-charcoal group-hover:text-green-700"
		>
			{recipe.name}
		</h3>

		<!-- Description -->
		{#if recipe.description}
			<p class="mt-2 line-clamp-2 text-sm leading-relaxed text-charcoal/60">
				{recipe.description}
			</p>
		{/if}

		<!-- Meta -->
		<div class="mt-4 flex items-center gap-4 text-xs text-charcoal/40">
			<!-- Ingredient count -->
			<span class="flex items-center gap-1">
				<svg
					xmlns="http://www.w3.org/2000/svg"
					class="h-3.5 w-3.5"
					fill="none"
					viewBox="0 0 24 24"
					stroke="currentColor"
					stroke-width="2"
				>
					<path
						stroke-linecap="round"
						stroke-linejoin="round"
						d="M8.25 6.75h12M8.25 12h12m-12 5.25h12M3.75 6.75h.007v.008H3.75V6.75Zm.375 0a.375.375 0 1 1-.75 0 .375.375 0 0 1 .75 0ZM3.75 12h.007v.008H3.75V12Zm.375 0a.375.375 0 1 1-.75 0 .375.375 0 0 1 .75 0Zm-.375 5.25h.007v.008H3.75v-.008Zm.375 0a.375.375 0 1 1-.75 0 .375.375 0 0 1 .75 0Z"
					/>
				</svg>
				{recipe.ingredients.length} ingredient{recipe.ingredients.length !== 1 ? 's' : ''}
			</span>

			<!-- Date -->
			<span>{formatDate(recipe.createdAt)}</span>
		</div>
	</div>

	<!-- Footer -->
	<div
		class="flex items-center justify-between border-t border-green-100/60 bg-green-50/30 px-5 py-3"
	>
		{#if safeSourceUrl}
			<a
				href={safeSourceUrl}
				target="_blank"
				rel="noopener noreferrer"
				class="flex items-center gap-1 text-xs font-medium text-green-600 transition-colors hover:text-green-700"
			>
				<svg
					xmlns="http://www.w3.org/2000/svg"
					class="h-3.5 w-3.5"
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
				Source
			</a>
		{:else}
			<span></span>
		{/if}
		<span class="text-xs text-charcoal/30">
			Updated {formatDate(recipe.updatedAt)}
		</span>
	</div>
</article>
