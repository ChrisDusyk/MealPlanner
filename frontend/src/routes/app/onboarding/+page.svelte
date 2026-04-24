<!-- Onboarding flow is implemented fully in Phase 5; this placeholder keeps
     typed routes aware of the path and sends authenticated users straight
     into the app while profile capture is still under construction. -->
<script lang="ts">
	import { goto } from '$app/navigation';
	import { resolve } from '$app/paths';
	import { onMount } from 'svelte';

	onMount(() => {
		void goto(resolve('/app'));
	});
</script>

<p class="p-6 text-sm text-green-800">Loading your workspace…</p>
