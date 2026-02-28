<script lang="ts">
	import { MEAL_CATEGORIES, WEEK_DAYS, getDateForDay } from '$lib/api/mealPlanApi';
	import type { DayPlan, MealSlotItem } from '$lib/api/mealPlanApi';
	import MealSlot from './MealSlot.svelte';

	let {
		days,
		weekStart,
		onAdd,
		onRemove,
		onCopy,
		onUpdateServings
	}: {
		days: DayPlan[];
		weekStart: string;
		onAdd: (day: string, category: string) => void;
		onRemove: (day: string, category: string, index: number) => void;
		onCopy: (day: string, category: string) => void;
		onUpdateServings: (day: string, category: string, index: number, servings: number) => void;
	} = $props();

	let monday = $derived(new Date(weekStart + 'T00:00:00'));

	let today = $derived(() => {
		const now = new Date();
		return now.toISOString().slice(0, 10);
	});

	function getDayDate(dayIndex: number): string {
		const d = getDateForDay(monday, dayIndex);
		return d.toLocaleDateString('en-US', { month: 'short', day: 'numeric' });
	}

	function isToday(dayIndex: number): boolean {
		const d = getDateForDay(monday, dayIndex);
		return d.toISOString().slice(0, 10) === today();
	}

	function getItems(day: DayPlan, category: string): MealSlotItem[] {
		return day.slots[category] ?? [];
	}
</script>

<!-- Desktop: horizontal 7-column grid -->
<div class="hidden md:block">
	<div class="grid grid-cols-7 gap-2">
		{#each WEEK_DAYS as dayName, dayIndex (dayName)}
			{@const dayPlan = days.find((d) => d.day === dayName)}
			<div
				class="flex flex-col gap-1.5 rounded-xl border p-2 transition-all {isToday(dayIndex)
					? 'border-green-400/60 bg-green-50/40 shadow-sm shadow-green-200/40'
					: 'border-green-200/30 bg-white/60'}"
			>
				<!-- Day header -->
				<div class="mb-1 text-center">
					<p
						class="font-display text-xs font-bold uppercase tracking-wider {isToday(dayIndex)
							? 'text-green-700'
							: 'text-charcoal/50'}"
					>
						{dayName.slice(0, 3)}
					</p>
					<p
						class="font-display text-sm font-semibold {isToday(dayIndex)
							? 'text-green-900'
							: 'text-charcoal/70'}"
					>
						{getDayDate(dayIndex)}
					</p>
				</div>

				<!-- Category slots -->
				{#each MEAL_CATEGORIES as category (category)}
					{#if dayPlan}
						<MealSlot
							day={dayName}
							{category}
							items={getItems(dayPlan, category)}
							dateLabel={getDayDate(dayIndex)}
							{onAdd}
							{onRemove}
							{onCopy}
						/>
					{/if}
				{/each}
			</div>
		{/each}
	</div>
</div>

<!-- Mobile: vertical day cards -->
<div class="flex flex-col gap-3 md:hidden">
	{#each WEEK_DAYS as dayName, dayIndex (dayName)}
		{@const dayPlan = days.find((d) => d.day === dayName)}
		{@const todayFlag = isToday(dayIndex)}
		<details
			class="group rounded-xl border transition-all {todayFlag
				? 'border-green-400/60 bg-green-50/40 shadow-sm'
				: 'border-green-200/30 bg-white/60'}"
			open={todayFlag}
		>
			<summary
				class="flex cursor-pointer items-center justify-between px-4 py-3 font-display text-sm font-bold {todayFlag
					? 'text-green-800'
					: 'text-charcoal/70'}"
			>
				<span>
					{dayName}
					<span class="ml-1.5 text-xs font-normal text-charcoal/40">{getDayDate(dayIndex)}</span>
				</span>
				<svg
					xmlns="http://www.w3.org/2000/svg"
					class="h-4 w-4 text-charcoal/30 transition-transform group-open:rotate-180"
					fill="none"
					viewBox="0 0 24 24"
					stroke="currentColor"
					stroke-width="2"
				>
					<path stroke-linecap="round" stroke-linejoin="round" d="M19.5 8.25l-7.5 7.5-7.5-7.5" />
				</svg>
			</summary>

			<div class="grid grid-cols-1 gap-2 px-3 pb-3 sm:grid-cols-2">
				{#each MEAL_CATEGORIES as category (category)}
					{#if dayPlan}
						<MealSlot
							day={dayName}
							{category}
							items={getItems(dayPlan, category)}
							dateLabel={getDayDate(dayIndex)}
							{onAdd}
							{onRemove}
							{onCopy}
							{onUpdateServings}
						/>
					{/if}
				{/each}
			</div>
		</details>
	{/each}
</div>
