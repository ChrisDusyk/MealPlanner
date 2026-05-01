<script lang="ts">
	import { resolve } from '$app/paths';
</script>

<svelte:head>
	<title>Admin | Meal Planner</title>
</svelte:head>

<div class="mx-auto max-w-5xl">
	<header class="mb-8">
		<h1 class="font-display text-2xl font-bold text-charcoal sm:text-3xl">Admin Area</h1>
		<p class="mt-1 text-charcoal/60">Manage application-wide content and configuration.</p>
	</header>

	<div class="grid gap-4 sm:grid-cols-2">
		<a
			href={resolve('/app/admin/catalogue')}
			class="group rounded-xl border border-green-200/60 bg-white p-5 shadow-sm transition-all hover:-translate-y-0.5 hover:shadow-md"
		>
			<h2 class="font-display text-lg font-semibold text-charcoal group-hover:text-green-700">
				Manage recipe catalogue
			</h2>
			<p class="mt-1 text-sm text-charcoal/60">
				Curate the public catalogue of recipes that any user can browse and add to their list.
			</p>
		</a>

		<a
			href={resolve('/app/admin/catalogue/tags')}
			class="group rounded-xl border border-green-200/60 bg-white p-5 shadow-sm transition-all hover:-translate-y-0.5 hover:shadow-md"
		>
			<h2 class="font-display text-lg font-semibold text-charcoal group-hover:text-green-700">
				Manage catalogue tags
			</h2>
			<p class="mt-1 text-sm text-charcoal/60">
				Define the tags that categorise catalogue recipes for browsing and filtering.
			</p>
		</a>
	</div>
</div>
