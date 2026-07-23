<script lang="ts">
	// Rendered when the `demo-banner` feature flag resolves to true. The value is
	// evaluated server-side (SSR) against flagd and passed in via page data, so
	// toggling the flag in the admin screen shows/hides this banner on reload.
	let { visible = false }: { visible?: boolean } = $props();
</script>

{#if visible}
	<div
		class="bg-green-600 px-4 py-2 text-center text-sm font-medium text-white"
		role="status"
	>
		🎉 The <code class="rounded bg-white/20 px-1">demo-banner</code> feature flag is on — served
		by flagd through OpenFeature.
	</div>
{/if}
