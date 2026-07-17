<script lang="ts">
	import { enhance } from '$app/forms';
	import { getContext } from 'svelte';
	import type { FamilyInvitationResponse, MyFamilyResponse } from '$lib/api/familyApi';
	import type { ActionData, PageData } from './$types';
	import { APP_USER_CONTEXT_KEY, type AppUserContextValue } from '$lib/context/appUserContext';

	let { data, form }: { data: PageData; form: ActionData } = $props();

	const appUserContext = getContext<AppUserContextValue | undefined>(APP_USER_CONTEXT_KEY);

	let submitting = $state(false);
	let successMessage = $state('');
	// svelte-ignore state_referenced_locally
	let name = $state(data.appUser?.name ?? data.session?.user?.name ?? '');

	// ── Family state ──
	// svelte-ignore state_referenced_locally
	let family = $state<MyFamilyResponse | null>(data.family ?? null);
	// svelte-ignore state_referenced_locally
	let incomingInvitations = $state<FamilyInvitationResponse[]>(data.incomingInvitations ?? []);
	let familyActionError = $state('');
	let familyActionSuccess = $state('');
	let inviteEmail = $state('');
	let familyName = $state('');
	let renamingFamily = $state(false);
	let sendingInvite = $state(false);
	let processingInvitationId = $state('');
	let removingMemberUserId = $state('');
	let transferTargetUserId = $state('');
	let transferring = $state(false);
	let leaving = $state(false);
	let deleting = $state(false);

	$effect(() => {
		familyName = family?.name ?? '';
	});

	function handleEnhance() {
		submitting = true;
		successMessage = '';

		return async ({ update }: { update: (options?: { reset?: boolean }) => Promise<void> }) => {
			submitting = false;
			await update({ reset: false });
			if (form?.user) {
				name = form.user.name;
				appUserContext?.setAppUser(form.user);
				successMessage = 'Account details saved.';
			}
		};
	}

	async function familyAction(body: Record<string, string>): Promise<unknown> {
		const response = await fetch('/app/account/family', {
			method: 'POST',
			headers: { 'Content-Type': 'application/json' },
			body: JSON.stringify(body)
		});
		const payload = await response.json().catch(() => ({}));
		if (!response.ok) {
			throw new Error(payload?.error || 'Family action failed.');
		}
		return payload;
	}

	async function refreshFamily(): Promise<void> {
		const response = await fetch('/app/account/family');
		if (!response.ok) {
			const body = await response.json().catch(() => ({}));
			throw new Error(body?.error || 'Failed to refresh family data.');
		}
		const payload = await response.json();
		family = payload.family ?? null;
		incomingInvitations = payload.incoming ?? [];
	}

	function resetFamilyMessages(): void {
		familyActionError = '';
		familyActionSuccess = '';
	}

	async function runFamilyAction(
		action: () => Promise<void>,
		successText: string
	): Promise<void> {
		resetFamilyMessages();
		try {
			await action();
			familyActionSuccess = successText;
		} catch (error) {
			familyActionError = error instanceof Error ? error.message : 'Family action failed.';
		}
	}

	async function renameCurrentFamily(event: SubmitEvent): Promise<void> {
		event.preventDefault();
		const trimmed = familyName.trim();
		if (!trimmed) {
			familyActionError = 'Family name is required.';
			return;
		}
		renamingFamily = true;
		await runFamilyAction(async () => {
			family = (await familyAction({ action: 'rename', name: trimmed })) as MyFamilyResponse;
		}, 'Family renamed.');
		renamingFamily = false;
	}

	async function sendInvitation(event: SubmitEvent): Promise<void> {
		event.preventDefault();
		const email = inviteEmail.trim();
		if (!email) {
			familyActionError = 'Email is required.';
			return;
		}
		sendingInvite = true;
		await runFamilyAction(async () => {
			await familyAction({ action: 'invite', email });
			inviteEmail = '';
			await refreshFamily();
		}, 'Invitation sent.');
		sendingInvite = false;
	}

	async function cancelInvitation(invitationId: string): Promise<void> {
		processingInvitationId = invitationId;
		await runFamilyAction(async () => {
			await familyAction({ action: 'cancel-invitation', invitationId });
			await refreshFamily();
		}, 'Invitation cancelled.');
		processingInvitationId = '';
	}

	async function respondToInvitation(
		invitationId: string,
		action: 'accept-invitation' | 'decline-invitation'
	): Promise<void> {
		processingInvitationId = invitationId;
		await runFamilyAction(
			async () => {
				await familyAction({ action, invitationId });
				await refreshFamily();
			},
			action === 'accept-invitation' ? 'You joined the family.' : 'Invitation declined.'
		);
		processingInvitationId = '';
	}

	async function removeMember(memberUserId: string): Promise<void> {
		if (!confirm('Remove this member? They will get their own personal family with copies of the recipes they contributed.')) {
			return;
		}
		removingMemberUserId = memberUserId;
		await runFamilyAction(async () => {
			family = (await familyAction({ action: 'remove-member', memberUserId })) as MyFamilyResponse;
		}, 'Member removed.');
		removingMemberUserId = '';
	}

	async function transferOwnership(event: SubmitEvent): Promise<void> {
		event.preventDefault();
		if (!transferTargetUserId) {
			familyActionError = 'Choose a member to transfer ownership to.';
			return;
		}
		if (!confirm('Transfer ownership of this family? You will remain a member but lose owner controls.')) {
			return;
		}
		transferring = true;
		await runFamilyAction(async () => {
			family = (await familyAction({
				action: 'transfer-ownership',
				newOwnerUserId: transferTargetUserId
			})) as MyFamilyResponse;
			transferTargetUserId = '';
		}, 'Ownership transferred.');
		transferring = false;
	}

	async function leaveCurrentFamily(): Promise<void> {
		if (!confirm('Leave this family? You will get your own personal family with copies of the recipes you contributed.')) {
			return;
		}
		leaving = true;
		await runFamilyAction(async () => {
			family = (await familyAction({ action: 'leave' })) as MyFamilyResponse;
		}, 'You left the family.');
		leaving = false;
	}

	async function deleteCurrentFamily(): Promise<void> {
		if (!confirm('Delete this family? The shared meal plans and grocery lists will be removed, and every member will get their own personal family with copies of the recipes they contributed.')) {
			return;
		}
		deleting = true;
		await runFamilyAction(async () => {
			family = (await familyAction({ action: 'delete' })) as MyFamilyResponse;
		}, 'Family deleted.');
		deleting = false;
	}

	const otherMembers = $derived((family?.members ?? []).filter((member) => !member.isOwner));
	const hasOtherMembers = $derived(otherMembers.length > 0);
</script>

<svelte:head>
	<title>Account — Simple Meal Planner</title>
</svelte:head>

<div class="mx-auto max-w-3xl">
	<div class="mb-8">
		<h1 class="font-display text-2xl font-bold text-charcoal sm:text-3xl">Account</h1>
		<p class="mt-1 text-charcoal/70">Manage your profile and family for Simple Meal Planner.</p>
	</div>

	<section
		class="rounded-2xl border border-green-200/60 bg-white p-6 shadow-sm"
		aria-labelledby="profile-section-heading"
	>
		<div class="mb-5">
			<h2 id="profile-section-heading" class="font-display text-lg font-semibold text-charcoal">
				Profile
			</h2>
			<p class="mt-1 text-sm text-charcoal/60">Update your display name used across the app.</p>
		</div>

		<form method="POST" use:enhance={handleEnhance} class="space-y-4">
			<div>
				<label for="name" class="mb-1 block text-sm font-medium text-charcoal">Name</label>
				<input
					id="name"
					name="name"
					type="text"
					required
					bind:value={name}
					placeholder="Enter your name"
					class="w-full rounded-lg border border-green-200 bg-white px-3 py-2 text-sm text-charcoal shadow-sm transition-colors focus:border-green-400 focus:ring-1 focus:ring-green-400 focus:outline-none"
				/>
			</div>

			{#if form?.error}
				<p
					class="rounded-lg border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700"
					role="alert"
				>
					{form.error}
				</p>
			{/if}

			{#if successMessage}
				<p
					class="rounded-lg border border-green-200 bg-green-50 px-3 py-2 text-sm text-green-700"
					role="status"
				>
					{successMessage}
				</p>
			{/if}

			<div class="flex justify-end">
				<button
					type="submit"
					disabled={submitting}
					class="rounded-lg bg-green-600 px-4 py-2 text-sm font-semibold text-white shadow-sm transition-colors hover:bg-green-700 disabled:cursor-not-allowed disabled:opacity-50"
				>
					{submitting ? 'Saving...' : 'Save'}
				</button>
			</div>
		</form>
	</section>

	<section
		class="mt-6 rounded-2xl border border-green-200/60 bg-white p-6 shadow-sm"
		aria-labelledby="family-section-heading"
	>
		<div class="mb-5">
			<h2 id="family-section-heading" class="font-display text-lg font-semibold text-charcoal">
				Family
			</h2>
			<p class="mt-1 text-sm text-charcoal/60">
				Your family shares one meal plan, grocery list, and recipe library.
			</p>
		</div>

		{#if familyActionError}
			<p
				class="mb-4 rounded-lg border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700"
				role="alert"
			>
				{familyActionError}
			</p>
		{/if}

		{#if familyActionSuccess}
			<p
				class="mb-4 rounded-lg border border-green-200 bg-green-50 px-3 py-2 text-sm text-green-700"
				role="status"
			>
				{familyActionSuccess}
			</p>
		{/if}

		{#if incomingInvitations.length > 0}
			<div class="mb-4 rounded-xl border border-amber-200 bg-amber-50 p-4">
				<h3 class="font-display text-sm font-semibold text-charcoal">Family Invitations</h3>
				<p class="mt-1 text-xs text-charcoal/60">
					Accepting an invitation moves you to that family. Your current family's meal plans stay
					behind, and recipes you contributed come with you.
				</p>
				<ul class="mt-3 space-y-2">
					{#each incomingInvitations as invitation (invitation.id)}
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
									onclick={() => respondToInvitation(invitation.id, 'accept-invitation')}
									disabled={processingInvitationId === invitation.id}
									class="rounded-md bg-green-600 px-3 py-1 text-xs font-semibold text-white transition-colors hover:bg-green-700 disabled:cursor-not-allowed disabled:opacity-50"
								>
									Accept
								</button>
								<button
									type="button"
									onclick={() => respondToInvitation(invitation.id, 'decline-invitation')}
									disabled={processingInvitationId === invitation.id}
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

		{#if family}
			{#if family.isOwner}
				<form class="mb-4 flex flex-col gap-3 sm:flex-row" onsubmit={renameCurrentFamily}>
					<label for="family-name" class="sr-only">Family name</label>
					<input
						id="family-name"
						type="text"
						required
						bind:value={familyName}
						placeholder="Family name"
						class="w-full rounded-lg border border-green-200 bg-white px-3 py-2 text-sm text-charcoal shadow-sm transition-colors focus:border-green-400 focus:ring-1 focus:ring-green-400 focus:outline-none"
					/>
					<button
						type="submit"
						disabled={renamingFamily}
						class="rounded-lg border border-green-300 px-4 py-2 text-sm font-semibold text-green-700 shadow-sm transition-colors hover:bg-green-50 disabled:cursor-not-allowed disabled:opacity-50"
					>
						{renamingFamily ? 'Renaming...' : 'Rename'}
					</button>
				</form>
			{:else}
				<p class="mb-4 text-sm font-semibold text-charcoal">{family.name}</p>
			{/if}

			<div class="flex flex-col gap-4">
				<div class="rounded-xl border border-green-100 p-4">
					<h3 class="font-display text-sm font-semibold text-charcoal">Members</h3>
					<ul class="mt-3 space-y-2">
						{#each family.members as member (member.userId)}
							<li
								class="flex items-center justify-between gap-2 rounded-lg border border-green-100 bg-green-50/50 px-3 py-2"
							>
								<div>
									<p class="text-sm font-semibold text-charcoal">
										{member.name}
										{#if member.isOwner}
											<span
												class="ml-1 rounded-full bg-green-600 px-2 py-0.5 text-[10px] font-semibold text-white"
											>
												Owner
											</span>
										{/if}
									</p>
									<p class="text-xs text-charcoal/60">{member.email ?? 'No email on profile'}</p>
								</div>
								{#if family.isOwner && !member.isOwner}
									<button
										type="button"
										onclick={() => removeMember(member.userId)}
										disabled={removingMemberUserId === member.userId}
										class="rounded-md border border-red-200 px-2 py-1 text-xs font-semibold text-red-700 transition-colors hover:bg-red-50 disabled:cursor-not-allowed disabled:opacity-50"
									>
										{removingMemberUserId === member.userId ? 'Removing...' : 'Remove'}
									</button>
								{/if}
							</li>
						{/each}
					</ul>
				</div>

				{#if family.isOwner}
					<div class="rounded-xl border border-green-100 p-4">
						<h3 class="font-display text-sm font-semibold text-charcoal">Invite Someone</h3>
						<p class="mt-1 text-xs text-charcoal/60">
							They need a Simple Meal Planner account with this email.
						</p>
						<form class="mt-3 flex flex-col gap-3 sm:flex-row" onsubmit={sendInvitation}>
							<label for="invite-email" class="sr-only">Invitee email</label>
							<input
								id="invite-email"
								type="email"
								required
								bind:value={inviteEmail}
								placeholder="partner@example.com"
								class="w-full rounded-lg border border-green-200 bg-white px-3 py-2 text-sm text-charcoal shadow-sm transition-colors focus:border-green-400 focus:ring-1 focus:ring-green-400 focus:outline-none"
							/>
							<button
								type="submit"
								disabled={sendingInvite}
								class="rounded-lg bg-green-600 px-4 py-2 text-sm font-semibold text-white shadow-sm transition-colors hover:bg-green-700 disabled:cursor-not-allowed disabled:opacity-50"
							>
								{sendingInvite ? 'Sending...' : 'Send Invite'}
							</button>
						</form>

						{#if family.pendingInvitations.length > 0}
							<ul class="mt-3 space-y-2">
								{#each family.pendingInvitations as invitation (invitation.id)}
									<li
										class="flex items-center justify-between gap-2 rounded-lg border border-green-100 bg-white px-3 py-2"
									>
										<div>
											<p class="text-sm font-semibold text-charcoal">{invitation.inviteeName}</p>
											<p class="text-xs text-charcoal/60">
												{invitation.inviteeEmail ?? 'No email on profile'} · Pending
											</p>
										</div>
										<button
											type="button"
											onclick={() => cancelInvitation(invitation.id)}
											disabled={processingInvitationId === invitation.id}
											class="rounded-md border border-red-200 px-2 py-1 text-xs font-semibold text-red-700 transition-colors hover:bg-red-50 disabled:cursor-not-allowed disabled:opacity-50"
										>
											{processingInvitationId === invitation.id ? 'Cancelling...' : 'Cancel'}
										</button>
									</li>
								{/each}
							</ul>
						{/if}
					</div>
				{/if}

				{#if family.isOwner && hasOtherMembers}
					<div class="rounded-xl border border-green-100 p-4">
						<h3 class="font-display text-sm font-semibold text-charcoal">Transfer Ownership</h3>
						<p class="mt-1 text-xs text-charcoal/60">
							Hand owner controls to another member. Required before you can leave the family.
						</p>
						<form class="mt-3 flex flex-col gap-3 sm:flex-row" onsubmit={transferOwnership}>
							<label for="transfer-target" class="sr-only">New owner</label>
							<select
								id="transfer-target"
								bind:value={transferTargetUserId}
								class="w-full rounded-lg border border-green-200 bg-white px-3 py-2 text-sm text-charcoal shadow-sm transition-colors focus:border-green-400 focus:ring-1 focus:ring-green-400 focus:outline-none"
							>
								<option value="" disabled>Choose a member</option>
								{#each otherMembers as member (member.userId)}
									<option value={member.userId}>{member.name}</option>
								{/each}
							</select>
							<button
								type="submit"
								disabled={transferring}
								class="rounded-lg border border-green-300 px-4 py-2 text-sm font-semibold text-green-700 shadow-sm transition-colors hover:bg-green-50 disabled:cursor-not-allowed disabled:opacity-50"
							>
								{transferring ? 'Transferring...' : 'Transfer'}
							</button>
						</form>
					</div>
				{/if}

				<div class="rounded-xl border border-red-100 p-4">
					<h3 class="font-display text-sm font-semibold text-red-700">Danger Zone</h3>
					<div class="mt-3 flex flex-col gap-2 sm:flex-row">
						{#if !family.isOwner}
							<button
								type="button"
								onclick={leaveCurrentFamily}
								disabled={leaving}
								class="rounded-lg border border-red-200 px-4 py-2 text-sm font-semibold text-red-700 transition-colors hover:bg-red-50 disabled:cursor-not-allowed disabled:opacity-50"
							>
								{leaving ? 'Leaving...' : 'Leave Family'}
							</button>
						{/if}
						{#if family.isOwner && hasOtherMembers}
							<button
								type="button"
								onclick={deleteCurrentFamily}
								disabled={deleting}
								class="rounded-lg border border-red-200 px-4 py-2 text-sm font-semibold text-red-700 transition-colors hover:bg-red-50 disabled:cursor-not-allowed disabled:opacity-50"
							>
								{deleting ? 'Deleting...' : 'Delete Family'}
							</button>
						{/if}
						{#if family.isOwner && !hasOtherMembers}
							<p class="text-xs text-charcoal/60">
								This is your personal family. Invite someone to start planning together.
							</p>
						{/if}
					</div>
				</div>
			</div>
		{/if}
	</section>
</div>
