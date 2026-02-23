<script lang="ts">
	import { tick } from 'svelte';
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
	let dialogEl: HTMLDivElement | null = $state(null);
	let previousFocus: HTMLElement | null = null;

	// Reset selections whenever modal opens
	$effect(() => {
		if (open) {
			previousFocus = document.activeElement instanceof HTMLElement ? document.activeElement : null;
			const init: Record<string, boolean> = {};
			for (const d of WEEK_DAYS) {
				init[d] = false;
			}
			selected = init;
			tick().then(() => {
				dialogEl?.focus();
			});
			return;
		}

		const elementToFocus = previousFocus;
		previousFocus = null;
		if (elementToFocus) {
			tick().then(() => {
				elementToFocus.focus();
			});
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

	function trapFocus(e: KeyboardEvent) {
		if (e.key !== 'Tab' || !dialogEl) return;

		const focusables = Array.from(
			dialogEl.querySelectorAll<HTMLElement>(
				'button:not([disabled]), [href], input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])'
			)
		);

		if (focusables.length === 0) {
			e.preventDefault();
			return;
		}

		const first = focusables[0];
		const last = focusables[focusables.length - 1];
		const active = document.activeElement;

		if (e.shiftKey && active === first) {
			e.preventDefault();
			last.focus();
			return;
		}

		if (!e.shiftKey && active === last) {
			e.preventDefault();
			first.focus();
		}
	}

	function handleKeydown(e: KeyboardEvent) {
		trapFocus(e);
		if (e.key === 'Escape') onClose();
	}
</script>

{#if open}
	<div class="fixed inset-0 z-50 flex items-center justify-center p-4">
		<button
			type="button"
			class="absolute inset-0 bg-black/30 backdrop-blur-sm"
			onclick={onClose}
			aria-label="Close"
		></button>

		<div
			bind:this={dialogEl}
			class="relative z-10 w-full max-w-sm overflow-hidden rounded-2xl border border-green-200/50 bg-white shadow-2xl shadow-green-900/10"
			role="dialog"
			tabindex="-1"
			aria-modal="true"
			aria-labelledby="copy-modal-title"
			aria-describedby="copy-modal-description"
			onkeydown={handleKeydown}
		>
			<!-- Header -->
			<div class="border-b border-green-100/60 px-5 py-4">
				<div class="flex items-center justify-between">
					<div>
						<h3 id="copy-modal-title" class="font-display text-lg font-bold text-charcoal">
							Copy {category}
						</h3>
						<p id="copy-modal-description" class="mt-0.5 text-xs text-charcoal/70">
							From {sourceDay} to other days
						</p>
					</div>
					<button
						type="button"
						onclick={onClose}
						aria-label="Close"
						class="flex h-8 w-8 items-center justify-center rounded-lg text-charcoal/60 transition-colors hover:bg-green-50 hover:text-charcoal"
					>
						<svg
							xmlns="http://www.w3.org/2000/svg"
							class="h-5 w-5"
							fill="none"
							viewBox="0 0 24 24"
							stroke="currentColor"
							stroke-width="2"
						>
							<path stroke-linecap="round" stroke-linejoin="round" d="M6 18L18 6M6 6l12 12" />
						</svg>
					</button>
				</div>
			</div>

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

			<!-- Footer -->
			<div class="flex items-center justify-end gap-2 border-t border-green-100/60 px-5 py-3">
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
			</div>
		</div>
	</div>
{/if}
