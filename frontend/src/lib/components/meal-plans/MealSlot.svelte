<script lang="ts">
	import type { MealSlotItem } from '$lib/api/mealPlanApi';

	let {
		day,
		category,
		items,
		dateLabel,
		onAdd,
		onRemove,
		onCopy
	}: {
		day: string;
		category: string;
		items: MealSlotItem[];
		dateLabel: string;
		onAdd: (day: string, category: string) => void;
		onRemove: (day: string, category: string, index: number) => void;
		onCopy: (day: string, category: string) => void;
	} = $props();

	let hasItems = $derived(items.length > 0);
</script>

<div
	class="group/slot rounded-lg border border-green-200/40 bg-white p-2.5 transition-all hover:border-green-300/60 hover:shadow-sm {hasItems
		? ''
		: 'border-dashed'}"
>
	<!-- Slot header -->
	<div class="mb-1.5 flex items-center justify-between">
		<span class="font-display text-[11px] font-semibold uppercase tracking-wider text-charcoal/40">
			{category}
		</span>
		<div
			class="flex items-center gap-0.5 opacity-100 md:opacity-0 md:transition-opacity md:group-hover/slot:opacity-100"
		>
			{#if hasItems}
				<button
					onclick={() => onCopy(day, category)}
					class="flex h-6 w-6 items-center justify-center rounded text-charcoal/30 transition-colors hover:bg-green-50 hover:text-green-600"
					aria-label="Copy {category} to other days"
					title="Copy to other days"
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
							d="M15.75 17.25v3.375c0 .621-.504 1.125-1.125 1.125h-9.75a1.125 1.125 0 0 1-1.125-1.125V7.875c0-.621.504-1.125 1.125-1.125H6.75a9.06 9.06 0 0 1 1.5.124m7.5 10.376h3.375c.621 0 1.125-.504 1.125-1.125V11.25c0-4.46-3.243-8.161-7.5-8.876a9.06 9.06 0 0 0-1.5-.124H9.375c-.621 0-1.125.504-1.125 1.125v3.5m7.5 10.375H9.375a1.125 1.125 0 0 1-1.125-1.125v-9.25m12 6.625v-1.875a3.375 3.375 0 0 0-3.375-3.375h-1.5a1.125 1.125 0 0 1-1.125-1.125v-1.5a3.375 3.375 0 0 0-3.375-3.375H9.75"
						/>
					</svg>
				</button>
			{/if}
			<button
				onclick={() => onAdd(day, category)}
				class="flex h-6 w-6 items-center justify-center rounded text-charcoal/30 transition-colors hover:bg-green-50 hover:text-green-600"
				aria-label="Add item to {category}"
				title="Add item"
			>
				<svg
					xmlns="http://www.w3.org/2000/svg"
					class="h-3.5 w-3.5"
					fill="none"
					viewBox="0 0 24 24"
					stroke="currentColor"
					stroke-width="2.5"
				>
					<path stroke-linecap="round" stroke-linejoin="round" d="M12 4.5v15m7.5-7.5h-15" />
				</svg>
			</button>
		</div>
	</div>

	<!-- Items -->
	{#if hasItems}
		<ul class="flex flex-col gap-1">
			{#each items as item, index (index)}
				<li
					class="group/item flex items-center gap-1.5 rounded-md px-1.5 py-1 transition-colors hover:bg-green-50/60"
				>
					{#if item.recipeId}
						<span
							class="inline-block h-1.5 w-1.5 shrink-0 rounded-full bg-green-500"
							aria-hidden="true"
						></span>
						<span class="flex-1 truncate text-xs text-charcoal/80">{item.name}</span>
						{#if item.servings > 1}
							<span class="shrink-0 rounded bg-green-100 px-1 text-[10px] font-medium text-green-700">{item.servings}×</span>
						{/if}
					{:else}
						<span
							class="inline-block h-1.5 w-1.5 shrink-0 rounded-full bg-charcoal/20"
							aria-hidden="true"
						></span>
						<span class="flex-1 truncate text-xs italic text-charcoal/50">{item.name}</span>
					{/if}
					<button
						onclick={() => onRemove(day, category, index)}
						class="flex h-5 w-5 shrink-0 items-center justify-center rounded text-charcoal/20 opacity-100 md:opacity-0 md:transition-all hover:bg-red-50 hover:text-red-400 md:group-hover/item:opacity-100"
						aria-label="Remove {item.name}"
					>
						<svg
							xmlns="http://www.w3.org/2000/svg"
							class="h-3 w-3"
							fill="none"
							viewBox="0 0 24 24"
							stroke="currentColor"
							stroke-width="2.5"
						>
							<path
								stroke-linecap="round"
								stroke-linejoin="round"
								d="M6 18L18 6M6 6l12 12"
							/>
						</svg>
					</button>
				</li>
			{/each}
		</ul>
	{:else}
		<button
			onclick={() => onAdd(day, category)}
			class="flex w-full items-center justify-center rounded-md py-2 text-[11px] text-charcoal/25 transition-colors hover:bg-green-50/50 hover:text-green-600/60"
		>
			+ Add
		</button>
	{/if}
</div>
