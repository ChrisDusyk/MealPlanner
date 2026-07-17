<script lang="ts">
	import AppSidebar from '$lib/components/AppSidebar.svelte';
	import Toast from '$lib/components/Toast.svelte';
	import InvitationBanner from '$lib/components/family/InvitationBanner.svelte';

	let { children, data } = $props();
</script>

<div class="flex min-h-[calc(100vh-72px)] md:ml-64">
	<AppSidebar session={data.session} />
	<section
		class="flex-1 overflow-y-auto bg-cream p-6 pl-16 md:p-8 lg:p-10"
		aria-label="Application content"
	>
		<InvitationBanner invitations={data.familyInvitations ?? []} />
		{@render children()}
	</section>
</div>

<Toast />
