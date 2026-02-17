<script lang="ts">
	import AppSidebar from '$lib/components/AppSidebar.svelte';

	let { children, data } = $props();
</script>

<div class="flex min-h-[calc(100vh-72px)] md:ml-64">
	<AppSidebar session={data.session} />
	<main class="flex-1 overflow-y-auto bg-cream p-6 md:p-8 lg:p-10">
		{@render children()}
	</main>
</div>
