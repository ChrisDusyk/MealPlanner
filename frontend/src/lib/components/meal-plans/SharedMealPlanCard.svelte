<script lang="ts">
	import { MEAL_CATEGORIES, WEEK_DAYS } from '$lib/api/mealPlanApi';
	import type { MealPlanResponse, MealSlotItem } from '$lib/api/mealPlanApi';
	import MealPlanGrid from './MealPlanGrid.svelte';

	let {
		shareId,
		ownerUserId,
		ownerName,
		ownerEmail,
		permission,
		mealPlan,
		onDismiss,
		onAdd,
		onRemove,
		onCopy
	}: {
		shareId: string;
		ownerUserId: string;
		ownerName: string;
		ownerEmail: string;
		permission: string;
		mealPlan: MealPlanResponse;
		onDismiss: (shareId: string) => void;
		onAdd?: (day: string, category: string) => void;
		onRemove?: (day: string, category: string, index: number) => void;
		onCopy?: (day: string, category: string) => void;
	} = $props();

	let expanded = $state(false);
	let canEdit = $derived(permission === 'ReadWrite' && !!onAdd && !!onRemove && !!onCopy);

	function getItems(dayName: string, category: string): MealSlotItem[] {
		const dayPlan = mealPlan.days.find((d) => d.day === dayName);
		return dayPlan?.slots[category] ?? [];
	}

	let totalItems = $derived(
		mealPlan.days.reduce(
			(sum, day) => sum + Object.values(day.slots).reduce((s, items) => s + items.length, 0),
			0
		)
	);
</script>

<div class="rounded-xl border border-blue-200/50 bg-blue-50/20">
	<!-- Card header — always visible -->
	<button
		onclick={() => (expanded = !expanded)}
		class="flex w-full items-center justify-between px-4 py-3 text-left"
	>
		<div class="flex items-center gap-3">
			<!-- Avatar circle -->
			<div
				class="flex h-8 w-8 items-center justify-center rounded-full bg-blue-100 text-xs font-bold text-blue-600"
			>
				{ownerName.charAt(0).toUpperCase()}
			</div>
			<div>
				<p class="font-display text-sm font-semibold text-charcoal">
					{ownerName}
					<span
						class="ml-1.5 inline-block rounded-full px-1.5 py-0.5 align-middle text-[10px] font-semibold leading-none {permission ===
						'ReadWrite'
							? 'bg-amber-100 text-amber-700'
							: 'bg-blue-100 text-blue-600'}"
					>
						{permission === 'ReadWrite' ? 'Can edit' : 'View only'}
					</span>
				</p>
				<p class="text-xs text-charcoal/40">{ownerEmail}</p>
			</div>
		</div>

		<div class="flex items-center gap-2">
			<span class="text-xs text-charcoal/40">
				{totalItems} {totalItems === 1 ? 'item' : 'items'}
			</span>
			<svg
				xmlns="http://www.w3.org/2000/svg"
				class="h-4 w-4 text-charcoal/30 transition-transform {expanded ? 'rotate-180' : ''}"
				fill="none"
				viewBox="0 0 24 24"
				stroke="currentColor"
				stroke-width="2"
			>
				<path stroke-linecap="round" stroke-linejoin="round" d="M19.5 8.25l-7.5 7.5-7.5-7.5" />
			</svg>
		</div>
	</button>

	{#if expanded}
		<div class="border-t border-blue-100/60 px-4 pb-4 pt-3">
			<!-- Dismiss button -->
			<div class="mb-3 flex justify-end">
				<button
					onclick={() => onDismiss(shareId)}
					class="rounded-lg px-3 py-1 text-xs font-medium text-charcoal/40 transition-colors hover:bg-red-50 hover:text-red-500"
				>
					Dismiss
				</button>
			</div>

			{#if canEdit}
				<!-- Full interactive grid for ReadWrite shares -->
				<MealPlanGrid
					days={mealPlan.days}
					weekStart={mealPlan.weekStart}
					onAdd={onAdd!}
					onRemove={onRemove!}
					onCopy={onCopy!}
				/>
			{:else}
				<!-- Compact read-only grid for ReadOnly shares -->
				<div class="overflow-x-auto">
					<div class="grid min-w-[640px] grid-cols-7 gap-1.5">
						{#each WEEK_DAYS as dayName (dayName)}
							<div class="flex flex-col gap-1">
								<p
									class="mb-0.5 text-center font-display text-[10px] font-bold uppercase tracking-wider text-charcoal/40"
								>
									{dayName.slice(0, 3)}
								</p>

								{#each MEAL_CATEGORIES as category (category)}
									{@const items = getItems(dayName, category)}
									<div
										class="rounded-lg px-1.5 py-1 {items.length > 0
											? 'bg-blue-50/60'
											: 'bg-transparent'}"
									>
										{#if items.length > 0}
											{#each items as item (item.name)}
												<p class="truncate text-[10px] text-charcoal/70" title={item.name}>
													{item.name}
												</p>
											{/each}
										{:else}
											<p class="text-[10px] text-charcoal/15">—</p>
										{/if}
									</div>
								{/each}
							</div>
						{/each}
					</div>
				</div>
			{/if}
		</div>
	{/if}
</div>
