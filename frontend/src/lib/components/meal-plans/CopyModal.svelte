<script lang="ts">
	import Modal from '$lib/components/Modal.svelte';
	import { WEEK_DAYS } from '$lib/api/mealPlanApi';

	let {
		open = false,
		sourceDay,
		category,
		onConfirm,
		onClose
	}: {
		open: boolean;
		sourceDay: string;
		category: string;
		onConfirm: (targetDays: string[]) => void;
		onClose: () => void;
	} = $props();

	let selected: Record<string, boolean> = $state({});

	// Reset selections whenever modal opens
	$effect(() => {
		if (open) {
			const init: Record<string, boolean> = {};
			for (const d of WEEK_DAYS) {
				init[d] = false;
			}
			selected = init;
		}
	});

	let otherDays = $derived(WEEK_DAYS.filter((d) => d !== sourceDay));
	let selectedCount = $derived(Object.values(selected).filter(Boolean).length);

	function toggleAll(days: string[]) {
		const allSelected = days.every((d) => selected[d]);
		for (const d of days) {
			selected[d] = !allSelected;
		}
	}

	function handleConfirm() {
		const targetDays = Object.entries(selected)
			.filter(([, v]) => v)
			.map(([k]) => k);
		if (targetDays.length > 0) {
			onConfirm(targetDays);
		}
		onClose();
	}
</script>

<Modal {open} {onClose} size="sm" title="Copy {category}" subtitle="From {sourceDay} to other days">
	<!-- Body -->
	<div class="px-5 py-4">
		<!-- Quick select shortcuts -->
		<div class="mb-3 flex gap-2">
			<button
				type="button"
				onclick={() => toggleAll(otherDays.filter((d) => !['Saturday', 'Sunday'].includes(d)))}
				class="min-h-10 rounded-full border border-green-200/50 bg-green-50/50 px-3 py-1 text-xs font-medium text-green-700 transition-all hover:border-green-300 hover:bg-green-100/50"
			>
				Weekdays
			</button>
			<button
				type="button"
				onclick={() => toggleAll(otherDays.filter((d) => ['Saturday', 'Sunday'].includes(d)))}
				class="min-h-10 rounded-full border border-green-200/50 bg-green-50/50 px-3 py-1 text-xs font-medium text-green-700 transition-all hover:border-green-300 hover:bg-green-100/50"
			>
				Weekend
			</button>
			<button
				type="button"
				onclick={() => toggleAll(otherDays)}
				class="min-h-10 rounded-full border border-green-200/50 bg-green-50/50 px-3 py-1 text-xs font-medium text-green-700 transition-all hover:border-green-300 hover:bg-green-100/50"
			>
				All Days
			</button>
		</div>

		<!-- Day checkboxes -->
		<ul class="flex flex-col gap-1">
			{#each otherDays as day (day)}
				<li>
					<label
						class="flex cursor-pointer items-center gap-3 rounded-lg px-3 py-2 transition-colors hover:bg-green-50/60"
					>
						<input
							type="checkbox"
							bind:checked={selected[day]}
							class="h-4 w-4 rounded border-green-300 text-green-600 focus:ring-green-400/30"
						/>
						<span class="text-sm text-charcoal">{day}</span>
					</label>
				</li>
			{/each}
		</ul>
	</div>

	{#snippet footer()}
		<button
			type="button"
			onclick={onClose}
			class="rounded-lg px-4 py-2 font-display text-xs font-semibold text-charcoal/80 transition-colors hover:bg-green-50 hover:text-charcoal"
		>
			Cancel
		</button>
		<button
			type="button"
			onclick={handleConfirm}
			disabled={selectedCount === 0}
			class="rounded-lg bg-green-600 px-4 py-2 font-display text-xs font-semibold text-white shadow-sm transition-all hover:bg-green-700 disabled:cursor-not-allowed disabled:opacity-40"
		>
			Copy to {selectedCount}
			{selectedCount === 1 ? 'day' : 'days'}
		</button>
	{/snippet}
</Modal>
