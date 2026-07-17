<script lang="ts">
	import { invalidateAll } from '$app/navigation';
	import type { FamilyInvitationResponse } from '$lib/api/familyApi';

	let { invitations }: { invitations: FamilyInvitationResponse[] } = $props();

	let processingId = $state('');
	let errorMessage = $state('');
	let dismissed = $state(false);

	async function respond(invitationId: string, action: 'accept-invitation' | 'decline-invitation') {
		processingId = invitationId;
		errorMessage = '';
		try {
			const response = await fetch('/app/account/family', {
				method: 'POST',
				headers: { 'Content-Type': 'application/json' },
				body: JSON.stringify({ action, invitationId })
			});
			if (!response.ok) {
				const body = await response.json().catch(() => ({}));
				throw new Error(body?.error || 'Failed to respond to the invitation.');
			}
			await invalidateAll();
		} catch (error) {
			errorMessage =
				error instanceof Error ? error.message : 'Failed to respond to the invitation.';
		} finally {
			processingId = '';
		}
	}
</script>

{#if invitations.length > 0 && !dismissed}
	<div
		class="mb-6 rounded-2xl border border-amber-200 bg-amber-50 p-4 shadow-sm"
		role="region"
		aria-label="Family invitations"
	>
		<div class="flex items-start justify-between gap-2">
			<div>
				<h2 class="font-display text-sm font-semibold text-charcoal">
					{invitations.length === 1 ? 'You have a family invitation' : 'You have family invitations'}
				</h2>
				<p class="mt-1 text-xs text-charcoal/60">
					Accepting moves you to that family — your current family's meal plans stay behind, and
					recipes you contributed come with you.
				</p>
			</div>
			<button
				type="button"
				onclick={() => (dismissed = true)}
				class="rounded-md px-2 py-1 text-xs font-semibold text-charcoal/50 transition-colors hover:bg-amber-100 hover:text-charcoal"
				aria-label="Hide invitations"
			>
				Hide
			</button>
		</div>

		{#if errorMessage}
			<p
				class="mt-2 rounded-lg border border-red-200 bg-red-50 px-3 py-2 text-xs text-red-700"
				role="alert"
			>
				{errorMessage}
			</p>
		{/if}

		<ul class="mt-3 space-y-2">
			{#each invitations as invitation (invitation.id)}
				<li
					class="flex flex-col gap-2 rounded-lg border border-amber-200 bg-white px-3 py-2 sm:flex-row sm:items-center sm:justify-between"
				>
					<div>
						<p class="text-sm font-semibold text-charcoal">{invitation.familyName}</p>
						<p class="text-xs text-charcoal/60">Invited by {invitation.inviterName}</p>
					</div>
					<div class="flex gap-2">
						<button
							type="button"
							onclick={() => respond(invitation.id, 'accept-invitation')}
							disabled={processingId === invitation.id}
							class="rounded-md bg-green-600 px-3 py-1 text-xs font-semibold text-white transition-colors hover:bg-green-700 disabled:cursor-not-allowed disabled:opacity-50"
						>
							Accept
						</button>
						<button
							type="button"
							onclick={() => respond(invitation.id, 'decline-invitation')}
							disabled={processingId === invitation.id}
							class="rounded-md border border-red-200 px-3 py-1 text-xs font-semibold text-red-700 transition-colors hover:bg-red-50 disabled:cursor-not-allowed disabled:opacity-50"
						>
							Decline
						</button>
					</div>
				</li>
			{/each}
		</ul>
	</div>
{/if}
