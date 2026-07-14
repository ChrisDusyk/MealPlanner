<script lang="ts">
	import { toast } from '$lib/stores/toast.svelte';
</script>

{#if toast.visible}
	<div
		role={toast.type === 'error' ? 'alert' : 'status'}
		aria-live={toast.type === 'error' ? 'assertive' : 'polite'}
		aria-atomic="true"
		class="fixed right-6 bottom-6 left-6 z-50 flex items-center gap-2 rounded-lg px-4 py-3 shadow-lg transition-all sm:left-auto {toast.type ===
		'error'
			? 'bg-red-600 text-white'
			: 'bg-green-700 text-white'}"
	>
		{#if toast.type === 'success'}
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
					d="M9 12.75L11.25 15 15 9.75M21 12a9 9 0 11-18 0 9 9 0 0118 0z"
				/>
			</svg>
		{:else}
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
					d="M12 9v3.75m9-.75a9 9 0 11-18 0 9 9 0 0118 0zm-9 3.75h.008v.008H12v-.008z"
				/>
			</svg>
		{/if}
		<span class="font-display text-sm font-medium">{toast.message}</span>
	</div>
{/if}
