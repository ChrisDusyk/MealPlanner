<script lang="ts">
	import { tick, type Snippet } from 'svelte';

	let {
		open = false,
		onClose,
		size = 'md',
		title,
		subtitle,
		onkeydown,
		children,
		headerExtra,
		footer
	}: {
		open?: boolean;
		onClose: () => void;
		size?: 'sm' | 'md';
		title: string;
		subtitle?: string;
		onkeydown?: (e: KeyboardEvent) => void;
		children: Snippet;
		headerExtra?: Snippet;
		footer?: Snippet;
	} = $props();

	const uid = $props.id();
	const titleId = `${uid}-title`;
	const subtitleId = `${uid}-subtitle`;

	const sizeClasses: Record<'sm' | 'md', string> = {
		sm: 'max-w-sm',
		md: 'max-w-md'
	};

	let dialogEl: HTMLDivElement | null = $state(null);
	let previousFocus: HTMLElement | null = null;

	$effect(() => {
		if (open) {
			previousFocus = document.activeElement instanceof HTMLElement ? document.activeElement : null;
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
		onkeydown?.(e);
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
			tabindex="-1"
		></button>

		<div
			bind:this={dialogEl}
			class="relative z-10 w-full {sizeClasses[
				size
			]} overflow-hidden rounded-2xl border border-green-200/50 bg-white shadow-2xl shadow-green-900/10"
			role="dialog"
			tabindex="-1"
			aria-modal="true"
			aria-labelledby={titleId}
			aria-describedby={subtitle ? subtitleId : undefined}
			onkeydown={handleKeydown}
		>
			<!-- Header -->
			<div class="border-b border-green-100/60 px-5 py-4">
				<div class="flex items-center justify-between">
					<div>
						<h3 id={titleId} class="font-display text-lg font-bold text-charcoal">
							{title}
						</h3>
						{#if subtitle}
							<p id={subtitleId} class="mt-0.5 text-xs text-charcoal/70">
								{subtitle}
							</p>
						{/if}
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

				{#if headerExtra}
					{@render headerExtra()}
				{/if}
			</div>

			{@render children()}

			{#if footer}
				<div class="flex items-center justify-end gap-2 border-t border-green-100/60 px-5 py-3">
					{@render footer()}
				</div>
			{/if}
		</div>
	</div>
{/if}
