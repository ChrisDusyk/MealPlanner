<script lang="ts">
	import { getDateForDay } from '$lib/api/mealPlanApi';

	let {
		weekStart,
		onNavigate
	}: {
		weekStart: string;
		onNavigate: (weekStart: string) => void;
	} = $props();

	let monday = $derived(new Date(weekStart + 'T00:00:00'));
	let sunday = $derived(getDateForDay(monday, 6));

	let weekLabel = $derived(formatRange(monday, sunday));

	function formatRange(start: Date, end: Date): string {
		const startMonth = start.toLocaleDateString('en-US', { month: 'short' });
		const endMonth = end.toLocaleDateString('en-US', { month: 'short' });
		const startDay = start.getDate();
		const endDay = end.getDate();
		const year = end.getFullYear();

		if (startMonth === endMonth) {
			return `${startMonth} ${startDay} – ${endDay}, ${year}`;
		}
		return `${startMonth} ${startDay} – ${endMonth} ${endDay}, ${year}`;
	}

	function navigateWeek(offset: number) {
		const d = new Date(monday);
		d.setDate(d.getDate() + offset * 7);
		onNavigate(d.toISOString().slice(0, 10));
	}

	function goToday() {
		const now = new Date();
		const day = now.getDay();
		const diff = day === 0 ? 6 : day - 1;
		now.setDate(now.getDate() - diff);
		onNavigate(now.toISOString().slice(0, 10));
	}
</script>

<div class="flex items-center justify-between gap-4">
	<div class="flex items-center gap-2">
		<button
			onclick={() => navigateWeek(-1)}
			class="flex h-9 w-9 items-center justify-center rounded-lg border border-green-200/60 bg-white text-charcoal/70 shadow-sm transition-all hover:border-green-300 hover:bg-green-50 hover:text-charcoal"
			aria-label="Previous week"
		>
			<svg
				xmlns="http://www.w3.org/2000/svg"
				class="h-4 w-4"
				fill="none"
				viewBox="0 0 24 24"
				stroke="currentColor"
				stroke-width="2.5"
			>
				<path stroke-linecap="round" stroke-linejoin="round" d="M15.75 19.5L8.25 12l7.5-7.5" />
			</svg>
		</button>

		<h2 class="min-w-0 text-center font-display text-base font-bold text-charcoal sm:min-w-[200px] sm:text-xl">
			{weekLabel}
		</h2>

		<button
			onclick={() => navigateWeek(1)}
			class="flex h-9 w-9 items-center justify-center rounded-lg border border-green-200/60 bg-white text-charcoal/70 shadow-sm transition-all hover:border-green-300 hover:bg-green-50 hover:text-charcoal"
			aria-label="Next week"
		>
			<svg
				xmlns="http://www.w3.org/2000/svg"
				class="h-4 w-4"
				fill="none"
				viewBox="0 0 24 24"
				stroke="currentColor"
				stroke-width="2.5"
			>
				<path stroke-linecap="round" stroke-linejoin="round" d="M8.25 4.5l7.5 7.5-7.5 7.5" />
			</svg>
		</button>
	</div>

	<button
		onclick={goToday}
		class="rounded-lg border border-green-200/60 bg-white px-3.5 py-1.5 font-display text-xs font-semibold text-green-700 shadow-sm transition-all hover:border-green-300 hover:bg-green-50"
	>
		Today
	</button>
</div>
