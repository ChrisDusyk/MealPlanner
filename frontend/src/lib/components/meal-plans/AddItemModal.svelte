<script lang="ts">
	import Modal from '$lib/components/Modal.svelte';
	import type { Recipe } from '$lib/api/recipeApi';
	import type { MealSlotItem } from '$lib/api/mealPlanApi';

	let {
		open = false,
		recipes,
		onSelect,
		onClose
	}: {
		open: boolean;
		recipes: Recipe[];
		onSelect: (item: MealSlotItem) => void;
		onClose: () => void;
	} = $props();

	let mode: 'recipes' | 'quick' = $state('recipes');
	let searchTerm = $state('');
	let quickText = $state('');
	let recipeServings: Record<string, number> = $state({});

	let filteredRecipes = $derived(
		searchTerm.trim()
			? recipes.filter((r) => r.name.toLowerCase().includes(searchTerm.toLowerCase()))
			: recipes
	);

	function selectRecipe(recipe: Recipe) {
		const servings = recipeServings[recipe.id] ?? 1;
		onSelect({ recipeId: recipe.id, name: recipe.name, servings });
		reset();
	}

	function addQuickEntry() {
		const text = quickText.trim();
		if (!text) return;
		onSelect({ recipeId: null, name: text, servings: 1 });
		reset();
	}

	function reset() {
		searchTerm = '';
		quickText = '';
		recipeServings = {};
		mode = 'recipes';
		onClose();
	}
</script>

<Modal {open} onClose={reset} title="Add Item">
	{#snippet headerExtra()}
		<!-- Tabs -->
		<div class="mt-3 flex gap-1 rounded-lg bg-green-50/80 p-1">
			<button
				type="button"
				onclick={() => (mode = 'recipes')}
				class="flex-1 rounded-md px-3 py-1.5 font-display text-xs font-semibold transition-all {mode ===
				'recipes'
					? 'bg-white text-green-700 shadow-sm'
					: 'text-charcoal/70 hover:text-charcoal'}"
			>
				My Recipes
			</button>
			<button
				type="button"
				onclick={() => (mode = 'quick')}
				class="flex-1 rounded-md px-3 py-1.5 font-display text-xs font-semibold transition-all {mode ===
				'quick'
					? 'bg-white text-green-700 shadow-sm'
					: 'text-charcoal/70 hover:text-charcoal'}"
			>
				Quick Entry
			</button>
		</div>
	{/snippet}

	<!-- Body -->
	<div class="max-h-[50vh] overflow-y-auto p-5">
		{#if mode === 'recipes'}
			<!-- Search -->
			<div class="mb-3">
				<input
					type="text"
					bind:value={searchTerm}
					placeholder="Search recipes…"
					class="w-full rounded-lg border border-green-200/60 bg-green-50/30 px-3 py-2 text-sm text-charcoal placeholder:text-charcoal/30 focus:border-green-400 focus:ring-2 focus:ring-green-400/20 focus:outline-none"
				/>
			</div>

			{#if filteredRecipes.length === 0}
				<p class="py-6 text-center text-sm text-charcoal/60">
					{searchTerm.trim()
						? 'No recipes match your search.'
						: 'No recipes yet. Create some first!'}
				</p>
			{:else}
				<ul class="flex flex-col gap-1">
					{#each filteredRecipes as recipe (recipe.id)}
						<li>
							<button
								type="button"
								onclick={() => selectRecipe(recipe)}
								class="flex w-full items-center gap-3 rounded-lg px-3 py-2.5 text-left transition-all hover:bg-green-50"
							>
								<span
									class="flex h-8 w-8 shrink-0 items-center justify-center rounded-lg bg-green-100 font-display text-xs font-bold text-green-700"
								>
									{recipe.name.charAt(0).toUpperCase()}
								</span>
								<div class="min-w-0 flex-1">
									<p class="truncate text-sm font-medium text-charcoal">
										{recipe.name}
									</p>
									{#if recipe.description}
										<p class="truncate text-xs text-charcoal/60">
											{recipe.description}
										</p>
									{/if}
								</div>
								<input
									type="number"
									min="1"
									step="1"
									value={recipeServings[recipe.id] ?? 1}
									oninput={(e) => {
										recipeServings[recipe.id] = Math.max(
											1,
											parseInt((e.target as HTMLInputElement).value) || 1
										);
									}}
									onclick={(e) => e.stopPropagation()}
									class="w-12 shrink-0 rounded border border-green-200/60 px-1.5 py-1 text-center text-xs text-charcoal focus:border-green-400 focus:ring-1 focus:ring-green-400/30 focus:outline-none"
									title="Servings"
								/>
								<svg
									xmlns="http://www.w3.org/2000/svg"
									class="h-4 w-4 shrink-0 text-green-400"
									fill="none"
									viewBox="0 0 24 24"
									stroke="currentColor"
									stroke-width="2"
								>
									<path
										stroke-linecap="round"
										stroke-linejoin="round"
										d="M12 4.5v15m7.5-7.5h-15"
									/>
								</svg>
							</button>
						</li>
					{/each}
				</ul>
			{/if}
		{:else}
			<!-- Quick entry -->
			<div class="space-y-3">
				<p class="text-xs text-charcoal/70">
					Add a quick entry like "Leftovers", "Eat out", or any custom text.
				</p>
				<div class="flex gap-2">
					<input
						type="text"
						bind:value={quickText}
						placeholder="e.g. Leftovers"
						onkeydown={(e) => {
							if (e.key === 'Enter') {
								e.preventDefault();
								addQuickEntry();
							}
						}}
						class="w-full flex-1 rounded-lg border border-green-200/60 bg-green-50/30 px-3 py-2 text-sm text-charcoal placeholder:text-charcoal/30 focus:border-green-400 focus:ring-2 focus:ring-green-400/20 focus:outline-none"
					/>
					<button
						type="button"
						onclick={addQuickEntry}
						disabled={!quickText.trim()}
						class="min-h-10 rounded-lg bg-green-600 px-4 py-2 font-display text-xs font-semibold text-white shadow-sm transition-all hover:bg-green-700 disabled:cursor-not-allowed disabled:opacity-40"
					>
						Add
					</button>
				</div>

				<!-- Quick suggestions -->
				<div class="flex flex-wrap gap-1.5">
					{#each ['Leftovers', 'Eat out', 'Skip', 'Smoothie', 'Snack bar'] as suggestion (suggestion)}
						<button
							type="button"
							onclick={() => {
								quickText = suggestion;
								addQuickEntry();
							}}
							class="min-h-10 rounded-full border border-green-200/50 bg-green-50/50 px-3 py-1 text-xs text-charcoal/80 transition-all hover:border-green-300 hover:bg-green-100/50 hover:text-charcoal"
						>
							{suggestion}
						</button>
					{/each}
				</div>
			</div>
		{/if}
	</div>
</Modal>
