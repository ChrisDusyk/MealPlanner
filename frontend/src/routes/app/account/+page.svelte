<script lang="ts">
	import { enhance } from '$app/forms';
	import { onMount } from 'svelte';
	import { getContext } from 'svelte';
	import type { FriendRequestSummary, FriendSummary } from '$lib/api/friendsApi';
	import { FriendsRealtimeClient } from '$lib/realtime/friendsRealtime';
	import type { ActionData, PageData } from './$types';
	import { APP_USER_CONTEXT_KEY, type AppUserContextValue } from '$lib/context/appUserContext';

	let { data, form }: { data: PageData; form: ActionData } = $props();

	const appUserContext = getContext<AppUserContextValue | undefined>(APP_USER_CONTEXT_KEY);

	let submitting = $state(false);
	let successMessage = $state('');
	let friendActionError = $state('');
	let friendActionSuccess = $state('');
	let sendingFriendRequest = $state(false);
	let processingRequestId = $state('');
	let removingFriendUserId = $state('');
	let savingFriendPreferenceUserId = $state('');
	let requestEmail = $state('');
	const realtimeClient = new FriendsRealtimeClient();
	// svelte-ignore state_referenced_locally
	let friends = $state<FriendSummary[]>(data.friends ?? []);
	// svelte-ignore state_referenced_locally
	let incomingFriendRequests = $state<FriendRequestSummary[]>(data.incomingFriendRequests ?? []);
	// svelte-ignore state_referenced_locally
	let outgoingFriendRequests = $state<FriendRequestSummary[]>(data.outgoingFriendRequests ?? []);
	// svelte-ignore state_referenced_locally
	let name = $state(data.appUser?.name ?? data.session?.user?.name ?? '');

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

	async function refreshFriends(): Promise<void> {
		const response = await fetch('/app/account/friends');
		if (!response.ok) {
			const body = await response.json().catch(() => ({}));
			throw new Error(body?.error || 'Failed to refresh friends data.');
		}

		const payload = await response.json();
		friends = payload.friends ?? [];
		incomingFriendRequests = payload.incoming ?? [];
		outgoingFriendRequests = payload.outgoing ?? [];
	}

	async function sendFriendRequest(event: SubmitEvent): Promise<void> {
		event.preventDefault();
		friendActionError = '';
		friendActionSuccess = '';

		const email = requestEmail.trim();
		if (!email) {
			friendActionError = 'Email is required.';
			return;
		}

		sendingFriendRequest = true;
		try {
			const response = await fetch('/app/account/friends', {
				method: 'POST',
				headers: {
					'Content-Type': 'application/json'
				},
				body: JSON.stringify({ action: 'send', email })
			});

			const body = await response.json().catch(() => ({}));
			if (!response.ok) {
				friendActionError = body.error || 'Failed to send friend request.';
				return;
			}

			requestEmail = '';
			friendActionSuccess =
				body.status === 'Accepted'
					? 'Friend request matched an existing request and was accepted.'
					: 'Friend request sent.';
			await refreshFriends();
		} catch (error) {
			friendActionError = error instanceof Error ? error.message : 'Failed to send friend request.';
		} finally {
			sendingFriendRequest = false;
		}
	}

	async function respondToFriendRequest(
		requestId: string,
		action: 'accept' | 'reject'
	): Promise<void> {
		friendActionError = '';
		friendActionSuccess = '';
		processingRequestId = requestId;

		try {
			const response = await fetch('/app/account/friends', {
				method: 'POST',
				headers: {
					'Content-Type': 'application/json'
				},
				body: JSON.stringify({ action, requestId })
			});

			const body = await response.json().catch(() => ({}));
			if (!response.ok) {
				friendActionError = body.error || 'Failed to update friend request.';
				return;
			}

			friendActionSuccess = action === 'accept' ? 'Friend request accepted.' : 'Friend request rejected.';
			await refreshFriends();
		} catch (error) {
			friendActionError = error instanceof Error ? error.message : 'Failed to update friend request.';
		} finally {
			processingRequestId = '';
		}
	}

	async function removeFriendship(friendUserId: string): Promise<void> {
		friendActionError = '';
		friendActionSuccess = '';
		removingFriendUserId = friendUserId;

		try {
			const response = await fetch(
				`/app/account/friends?friendUserId=${encodeURIComponent(friendUserId)}`,
				{ method: 'DELETE' }
			);
			const body = await response.json().catch(() => ({}));
			if (!response.ok) {
				friendActionError = body.error || 'Failed to remove friend.';
				return;
			}

			friendActionSuccess = 'Friend removed.';
			await refreshFriends();
		} catch (error) {
			friendActionError = error instanceof Error ? error.message : 'Failed to remove friend.';
		} finally {
			removingFriendUserId = '';
		}
	}

	async function updateFriendPreference(
		friend: FriendSummary,
		updates: Partial<Pick<FriendSummary, 'autoShareMealPlans' | 'autoShareGroceryLists'>>
	): Promise<void> {
		friendActionError = '';
		friendActionSuccess = '';
		savingFriendPreferenceUserId = friend.userId;

		const payload = {
			friendUserId: friend.userId,
			autoShareMealPlans: updates.autoShareMealPlans ?? friend.autoShareMealPlans,
			autoShareGroceryLists: updates.autoShareGroceryLists ?? friend.autoShareGroceryLists
		};

		try {
			const response = await fetch('/app/account/friends', {
				method: 'PATCH',
				headers: {
					'Content-Type': 'application/json'
				},
				body: JSON.stringify(payload)
			});

			const body = await response.json().catch(() => ({}));
			if (!response.ok) {
				friendActionError = body.error || 'Failed to update sharing preferences.';
				return;
			}

			friends = friends.map((item) =>
				item.userId === friend.userId
					? {
						...item,
						autoShareMealPlans: body.autoShareMealPlans,
						autoShareGroceryLists: body.autoShareGroceryLists
					}
					: item
			);
			friendActionSuccess = 'Sharing preferences updated.';
		} catch (error) {
			friendActionError = error instanceof Error ? error.message : 'Failed to update sharing preferences.';
		} finally {
			savingFriendPreferenceUserId = '';
		}
	}

	onMount(() => {
		let disposed = false;

		void realtimeClient
			.start(() => {
				if (disposed) return;
				void refreshFriends().catch(() => {
					// Keep local UI stable if a transient refresh fails.
				});
			})
			.catch((error) => {
				console.error('Failed to start friends realtime connection', error);
			});

		return () => {
			disposed = true;
			void realtimeClient.stop();
		};
	});
</script>

<svelte:head>
	<title>Account — Simple Meal Planner</title>
</svelte:head>

<div class="mx-auto max-w-3xl">
	<div class="mb-8">
		<h1 class="font-display text-2xl font-bold text-charcoal sm:text-3xl">Account</h1>
		<p class="mt-1 text-charcoal/70">Manage your profile settings for Simple Meal Planner.</p>
	</div>

	<section class="rounded-2xl border border-green-200/60 bg-white p-6 shadow-sm" aria-labelledby="profile-section-heading">
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
				<p class="rounded-lg border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700" role="alert">
					{form.error}
				</p>
			{/if}

			{#if successMessage}
				<p class="rounded-lg border border-green-200 bg-green-50 px-3 py-2 text-sm text-green-700" role="status">
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

	<section class="mt-6 rounded-2xl border border-green-200/60 bg-white p-6 shadow-sm" aria-labelledby="friends-section-heading">
		<div class="mb-5">
			<h2 id="friends-section-heading" class="font-display text-lg font-semibold text-charcoal">
				Friends
			</h2>
			<p class="mt-1 text-sm text-charcoal/60">
				Send requests by email and manage pending requests and friends.
			</p>
		</div>

		<form class="mb-4 flex flex-col gap-3 sm:flex-row" onsubmit={sendFriendRequest}>
			<label for="friend-request-email" class="sr-only">Friend email</label>
			<input
				id="friend-request-email"
				type="email"
				required
				bind:value={requestEmail}
				placeholder="friend@example.com"
				class="w-full rounded-lg border border-green-200 bg-white px-3 py-2 text-sm text-charcoal shadow-sm transition-colors focus:border-green-400 focus:ring-1 focus:ring-green-400 focus:outline-none"
			/>
			<button
				type="submit"
				disabled={sendingFriendRequest}
				class="rounded-lg bg-green-600 px-4 py-2 text-sm font-semibold text-white shadow-sm transition-colors hover:bg-green-700 disabled:cursor-not-allowed disabled:opacity-50"
			>
				{sendingFriendRequest ? 'Sending...' : 'Send Request'}
			</button>
		</form>

		{#if friendActionError}
			<p class="mb-4 rounded-lg border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700" role="alert">
				{friendActionError}
			</p>
		{/if}

		{#if friendActionSuccess}
			<p class="mb-4 rounded-lg border border-green-200 bg-green-50 px-3 py-2 text-sm text-green-700" role="status">
				{friendActionSuccess}
			</p>
		{/if}

		<div class="flex flex-col gap-4">
			<div class="rounded-xl border border-green-100 p-4">
				<h3 class="font-display text-sm font-semibold text-charcoal">Friends</h3>
				{#if friends.length === 0}
					<p class="mt-2 text-sm text-charcoal/60">No friends yet.</p>
				{:else}
					<ul class="mt-3 space-y-2">
						{#each friends as friend (friend.userId)}
							<li class="rounded-lg border border-green-100 bg-green-50/50 px-3 py-2">
								<div class="flex items-start justify-between gap-2">
									<div class="space-y-2">
										<p class="text-sm font-semibold text-charcoal">{friend.name}</p>
										<p class="text-xs text-charcoal/60">{friend.email ?? 'No email on profile'}</p>
										<div class="space-y-1">
											<label class="flex items-center gap-2 text-xs text-charcoal/80">
												<input
													type="checkbox"
													checked={friend.autoShareMealPlans}
													disabled={savingFriendPreferenceUserId === friend.userId}
													onchange={(event) =>
														updateFriendPreference(friend, {
															autoShareMealPlans: (event.currentTarget as HTMLInputElement).checked
														})}
													class="h-4 w-4 rounded border-green-300 text-green-600 focus:ring-green-500"
												/>
												<span>Auto-share new meal plans</span>
											</label>
											<label class="flex items-center gap-2 text-xs text-charcoal/80">
												<input
													type="checkbox"
													checked={friend.autoShareGroceryLists}
													disabled={savingFriendPreferenceUserId === friend.userId}
													onchange={(event) =>
														updateFriendPreference(friend, {
															autoShareGroceryLists: (event.currentTarget as HTMLInputElement).checked
														})}
													class="h-4 w-4 rounded border-green-300 text-green-600 focus:ring-green-500"
												/>
												<span>Auto-share new grocery lists</span>
											</label>
										</div>
									</div>
									<button
										type="button"
										onclick={() => removeFriendship(friend.userId)}
										disabled={removingFriendUserId === friend.userId}
										class="rounded-md border border-red-200 px-2 py-1 text-xs font-semibold text-red-700 transition-colors hover:bg-red-50 disabled:cursor-not-allowed disabled:opacity-50"
									>
										{removingFriendUserId === friend.userId ? 'Removing...' : 'Remove'}
									</button>
								</div>
							</li>
						{/each}
					</ul>
				{/if}
			</div>

			<div class="rounded-xl border border-green-100 p-4">
				<h3 class="font-display text-sm font-semibold text-charcoal">Incoming Requests</h3>
				{#if incomingFriendRequests.length === 0}
					<p class="mt-2 text-sm text-charcoal/60">No incoming requests.</p>
				{:else}
					<ul class="mt-3 space-y-2">
						{#each incomingFriendRequests as request (request.requestId)}
							<li class="rounded-lg border border-green-100 bg-white px-3 py-2">
								<p class="text-sm font-semibold text-charcoal">{request.name}</p>
								<p class="text-xs text-charcoal/60">{request.email ?? 'No email on profile'}</p>
								<div class="mt-2 flex gap-2">
									<button
										type="button"
										onclick={() => respondToFriendRequest(request.requestId, 'accept')}
										disabled={processingRequestId === request.requestId}
										class="rounded-md bg-green-600 px-2 py-1 text-xs font-semibold text-white transition-colors hover:bg-green-700 disabled:cursor-not-allowed disabled:opacity-50"
									>
										Accept
									</button>
									<button
										type="button"
										onclick={() => respondToFriendRequest(request.requestId, 'reject')}
										disabled={processingRequestId === request.requestId}
										class="rounded-md border border-red-200 px-2 py-1 text-xs font-semibold text-red-700 transition-colors hover:bg-red-50 disabled:cursor-not-allowed disabled:opacity-50"
									>
										Reject
									</button>
								</div>
							</li>
						{/each}
					</ul>
				{/if}
			</div>

			<div class="rounded-xl border border-green-100 p-4">
				<h3 class="font-display text-sm font-semibold text-charcoal">Outgoing Requests</h3>
				{#if outgoingFriendRequests.length === 0}
					<p class="mt-2 text-sm text-charcoal/60">No outgoing requests.</p>
				{:else}
					<ul class="mt-3 space-y-2">
						{#each outgoingFriendRequests as request (request.requestId)}
							<li class="rounded-lg border border-green-100 bg-white px-3 py-2">
								<p class="text-sm font-semibold text-charcoal">{request.name}</p>
								<p class="text-xs text-charcoal/60">{request.email ?? 'No email on profile'}</p>
								<p class="mt-1 text-xs text-charcoal/50">Pending</p>
							</li>
						{/each}
					</ul>
				{/if}
			</div>
		</div>
	</section>
</div>
