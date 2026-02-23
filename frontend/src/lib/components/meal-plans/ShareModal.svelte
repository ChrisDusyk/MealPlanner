<script lang="ts">
	import type { MealPlanShareResponse } from '$lib/api/sharingApi';

	let {
		open = false,
		weekStart,
		shares = [],
		onShare,
		onRevoke,
		onClose
	}: {
		open: boolean;
		weekStart: string;
		shares: MealPlanShareResponse[];
		onShare: (email: string, permission: string) => Promise<string | null>;
		onRevoke: (shareId: string) => Promise<void>;
		onClose: () => void;
	} = $props();

	let email = $state('');
	let permission: 'ReadOnly' | 'ReadWrite' = $state('ReadOnly');
	let errorMessage = $state('');
	let successMessage = $state('');
	let loading = $state(false);

	function reset() {
		email = '';
		permission = 'ReadOnly';
		errorMessage = '';
		successMessage = '';
		loading = false;
		onClose();
	}

	async function handleShare() {
		errorMessage = '';
		successMessage = '';

		const trimmed = email.trim();
		if (!trimmed) {
			errorMessage = 'Please enter an email address.';
			return;
		}

		loading = true;
		const error = await onShare(trimmed, permission);
		loading = false;

		if (error) {
			errorMessage = error;
		} else {
			successMessage = `Shared with ${trimmed}!`;
			email = '';
			setTimeout(() => {
				successMessage = '';
			}, 3000);
		}
	}

	async function handleRevoke(shareId: string) {
		await onRevoke(shareId);
	}

	function handleKeydown(e: KeyboardEvent) {
		if (e.key === 'Escape') reset();
		if (e.key === 'Enter' && !loading) handleShare();
	}
</script>

{#if open}
	<div class="fixed inset-0 z-50 flex items-center justify-center p-4">
		<button
			class="absolute inset-0 bg-black/30 backdrop-blur-sm"
			onclick={reset}
			aria-label="Close"
			tabindex="-1"
		></button>

		<!-- svelte-ignore a11y_no_noninteractive_element_interactions -->
		<div
			class="relative z-10 w-full max-w-md overflow-hidden rounded-2xl border border-green-200/50 bg-white shadow-2xl shadow-green-900/10"
			role="dialog"
			aria-label="Share meal plan"
			tabindex="-1"
			onkeydown={handleKeydown}
		>
			<!-- Header -->
			<div class="border-b border-green-100/60 px-5 py-4">
				<div class="flex items-center justify-between">
					<div>
						<h3 class="font-display text-lg font-bold text-charcoal">Share Meal Plan</h3>
						<p class="mt-0.5 text-xs text-charcoal/50">
							Week of {weekStart}
						</p>
					</div>
					<button
						onclick={reset}
						aria-label="Close"
						class="flex h-8 w-8 items-center justify-center rounded-lg text-charcoal/40 transition-colors hover:bg-green-50 hover:text-charcoal"
					>
						<svg
							xmlns="http://www.w3.org/2000/svg"
							class="h-5 w-5"
							fill="none"
							viewBox="0 0 24 24"
							stroke="currentColor"
							stroke-width="2"
						>
							<path
								stroke-linecap="round"
								stroke-linejoin="round"
								d="M6 18L18 6M6 6l12 12"
							/>
						</svg>
					</button>
				</div>
			</div>

			<!-- Body -->
			<div class="px-5 py-4">
				<!-- Share form -->
				<div class="space-y-3">
					<div>
						<label for="share-email" class="mb-1 block text-xs font-medium text-charcoal/70">
							Recipient email
						</label>
						<input
							id="share-email"
							type="email"
							bind:value={email}
							placeholder="name@example.com"
							disabled={loading}
							class="w-full rounded-lg border border-green-200/60 bg-white px-3 py-2 text-sm text-charcoal placeholder:text-charcoal/30 focus:border-green-400 focus:ring-2 focus:ring-green-400/20 focus:outline-none disabled:opacity-50"
						/>
					</div>

					<div>
						<span class="mb-1 block text-xs font-medium text-charcoal/70">Permission</span>
						<div class="flex gap-1 rounded-lg bg-green-50/80 p-1" role="group" aria-label="Permission">
							<button
								onclick={() => (permission = 'ReadOnly')}
								class="flex-1 rounded-md px-3 py-1.5 font-display text-xs font-semibold transition-all {permission ===
								'ReadOnly'
									? 'bg-white text-green-700 shadow-sm'
									: 'text-charcoal/50 hover:text-charcoal'}"
							>
								View only
							</button>
							<button
								onclick={() => (permission = 'ReadWrite')}
								class="flex-1 rounded-md px-3 py-1.5 font-display text-xs font-semibold transition-all {permission ===
								'ReadWrite'
									? 'bg-white text-green-700 shadow-sm'
									: 'text-charcoal/50 hover:text-charcoal'}"
							>
								Can edit
							</button>
						</div>
					</div>
				</div>

				<!-- Messages -->
				{#if errorMessage}
					<div class="mt-3 rounded-lg bg-red-50 px-3 py-2 text-xs text-red-600">
						{errorMessage}
					</div>
				{/if}
				{#if successMessage}
					<div class="mt-3 rounded-lg bg-green-50 px-3 py-2 text-xs text-green-700">
						{successMessage}
					</div>
				{/if}

				<!-- Share button -->
				<div class="mt-4">
					<button
						onclick={handleShare}
						disabled={loading || !email.trim()}
						class="w-full rounded-lg bg-green-600 px-4 py-2.5 font-display text-sm font-semibold text-white shadow-sm transition-all hover:bg-green-700 disabled:cursor-not-allowed disabled:opacity-40"
					>
						{#if loading}
							Sharing…
						{:else}
							Share
						{/if}
					</button>
				</div>

				<!-- Existing shares -->
				{#if shares.length > 0}
					<div class="mt-5 border-t border-green-100/60 pt-4">
						<h4 class="mb-2 text-xs font-semibold text-charcoal/60 uppercase tracking-wider">
							Shared with
						</h4>
						<ul class="space-y-2">
							{#each shares as share (share.id)}
								<li
									class="flex items-center justify-between rounded-lg border border-green-100/50 bg-green-50/30 px-3 py-2"
								>
									<div class="min-w-0 flex-1">
										<p class="truncate text-sm font-medium text-charcoal">
											{share.sharedWithName}
										</p>
										<p class="truncate text-xs text-charcoal/50">
											{share.sharedWithEmail}
										</p>
									</div>
									<div class="ml-2 flex items-center gap-2">
										<span
											class="rounded-full px-2 py-0.5 text-[10px] font-semibold {share.permission ===
											'ReadWrite'
												? 'bg-amber-100 text-amber-700'
												: 'bg-green-100 text-green-700'}"
										>
											{share.permission === 'ReadWrite' ? 'Edit' : 'View'}
										</span>
										<button
											onclick={() => handleRevoke(share.id)}
											aria-label="Revoke share with {share.sharedWithName}"
											class="flex h-6 w-6 items-center justify-center rounded text-charcoal/30 transition-colors hover:bg-red-50 hover:text-red-500"
										>
											<svg
												xmlns="http://www.w3.org/2000/svg"
												class="h-4 w-4"
												fill="none"
												viewBox="0 0 24 24"
												stroke="currentColor"
												stroke-width="2"
											>
												<path
													stroke-linecap="round"
													stroke-linejoin="round"
													d="M6 18L18 6M6 6l12 12"
												/>
											</svg>
										</button>
									</div>
								</li>
							{/each}
						</ul>
					</div>
				{/if}
			</div>
		</div>
	</div>
{/if}
