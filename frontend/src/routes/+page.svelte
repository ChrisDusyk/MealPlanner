<script lang="ts">
	import { onMount } from 'svelte';

	let mounted = $state(false);

	onMount(() => {
		mounted = true;

		// Intersection observer for scroll-triggered reveals
		const observer = new IntersectionObserver(
			(entries) => {
				entries.forEach((entry) => {
					if (entry.isIntersecting) {
						entry.target.classList.add('reveal');
						observer.unobserve(entry.target);
					}
				});
			},
			{ threshold: 0.1 }
		);

		document.querySelectorAll('.reveal-on-scroll').forEach((el) => {
			observer.observe(el);
		});

		return () => observer.disconnect();
	});
</script>

<!-- ═══════════════════════════════════════════════════════════ HERO -->
<section
	class="grain relative overflow-hidden bg-gradient-to-b from-green-900 via-green-800 to-green-700"
>
	<!-- Decorative circles -->
	<div class="absolute -top-32 -right-32 h-96 w-96 rounded-full bg-green-500/10 blur-3xl"></div>
	<div class="absolute -bottom-24 -left-24 h-72 w-72 rounded-full bg-green-400/10 blur-3xl"></div>

	<div
		class="relative mx-auto grid max-w-7xl gap-12 px-6 py-24 md:py-32 lg:grid-cols-2 lg:items-center lg:gap-16 lg:py-40"
	>
		<!-- Copy -->
		<div class="max-w-xl {mounted ? 'reveal' : 'opacity-0'}">
			<span
				class="mb-4 inline-block rounded-full border border-green-400/30 bg-green-800/40 px-4 py-1.5 font-display text-xs font-semibold tracking-widest text-green-300 uppercase"
			>
				Meal planning made simple
			</span>
			<h1
				class="mb-6 font-display text-4xl leading-tight font-extrabold tracking-tight text-white sm:text-5xl lg:text-6xl"
			>
				Plan meals.<br />
				Shop smarter.<br />
				<span class="text-green-300">Eat better.</span>
			</h1>
			<p class="mb-8 max-w-lg text-lg leading-relaxed text-green-100/80">
				Organize your weekly meals, browse your recipe collection, and auto-generate a grocery list
				— all in one place. Spend less time wondering "what's for dinner?" and more time enjoying
				it.
			</p>
			<div class="flex flex-wrap gap-4">
				<a
					href="/signup"
					class="rounded-xl bg-green-500 px-7 py-3.5 font-display text-sm font-bold text-white shadow-lg shadow-green-900/40 transition-all hover:-translate-y-0.5 hover:bg-green-400 hover:shadow-xl hover:shadow-green-900/50"
				>
					Start Free →
				</a>
				<a
					href="#how-it-works"
					class="rounded-xl border border-green-400/30 px-7 py-3.5 font-display text-sm font-semibold text-green-100 transition-all hover:border-green-400/60 hover:bg-green-800/40"
				>
					See How It Works
				</a>
			</div>
		</div>

		<!-- Hero Illustration (inline SVG scene) -->
		<div
			class="relative hidden items-center justify-center lg:flex {mounted
				? 'reveal reveal-delay-2'
				: 'opacity-0'}"
		>
			<div class="relative">
				<!-- Calendar card -->
				<div
					class="rounded-2xl border border-green-400/20 bg-green-800/40 p-6 shadow-2xl shadow-green-950/30 backdrop-blur-sm"
				>
					<div class="mb-4 flex items-center justify-between">
						<span class="font-display text-sm font-semibold text-green-200">This Week</span>
						<span class="font-display text-xs text-green-400">Feb 17 – 23</span>
					</div>
					<div class="grid grid-cols-7 gap-2">
						{#each ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun'] as day}
							<div class="text-center">
								<span class="block font-display text-[10px] font-medium text-green-400/70"
									>{day}</span
								>
								<div
									class="mt-1 h-14 rounded-lg {day === 'Wed'
										? 'border-2 border-green-400 bg-green-600/40'
										: 'bg-green-700/40'} flex items-center justify-center"
								>
									<svg
										xmlns="http://www.w3.org/2000/svg"
										class="h-5 w-5 {day === 'Wed' ? 'text-green-300' : 'text-green-500/40'}"
										fill="none"
										viewBox="0 0 24 24"
										stroke="currentColor"
										stroke-width="1.5"
									>
										<path
											stroke-linecap="round"
											stroke-linejoin="round"
											d="M12 6v12m-3-2.818.879.659c1.171.879 3.07.879 4.242 0 1.172-.879 1.172-2.303 0-3.182C13.536 12.219 12.768 12 12 12c-.725 0-1.45-.22-2.003-.659-1.106-.879-1.106-2.303 0-3.182s2.9-.879 4.006 0l.415.33M21 12a9 9 0 1 1-18 0 9 9 0 0 1 18 0Z"
										/>
									</svg>
								</div>
							</div>
						{/each}
					</div>
					<!-- Mini meal entries -->
					<div class="mt-4 space-y-2">
						<div class="flex items-center gap-3 rounded-lg bg-green-700/30 px-3 py-2">
							<div class="h-2 w-2 rounded-full bg-green-400"></div>
							<span class="font-display text-xs text-green-200">Chicken Stir-Fry</span>
							<span class="ml-auto font-display text-[10px] text-green-400/60">Dinner</span>
						</div>
						<div class="flex items-center gap-3 rounded-lg bg-green-700/30 px-3 py-2">
							<div class="h-2 w-2 rounded-full bg-green-300"></div>
							<span class="font-display text-xs text-green-200">Greek Salad Bowl</span>
							<span class="ml-auto font-display text-[10px] text-green-400/60">Lunch</span>
						</div>
						<div class="flex items-center gap-3 rounded-lg bg-green-700/30 px-3 py-2">
							<div class="h-2 w-2 rounded-full bg-yellow-400"></div>
							<span class="font-display text-xs text-green-200">Banana Pancakes</span>
							<span class="ml-auto font-display text-[10px] text-green-400/60">Breakfast</span>
						</div>
					</div>
				</div>

				<!-- Floating grocery list card -->
				<div
					class="absolute -right-12 -bottom-8 w-48 rounded-xl border border-green-400/20 bg-green-800/60 p-4 shadow-xl backdrop-blur-sm"
				>
					<span
						class="block font-display text-[10px] font-semibold tracking-wider text-green-300 uppercase"
						>Grocery List</span
					>
					<div class="mt-2 space-y-1.5">
						<div class="flex items-center gap-2">
							<div class="h-3 w-3 rounded border border-green-500/50"></div>
							<span class="text-xs text-green-200/80">Chicken breast</span>
						</div>
						<div class="flex items-center gap-2">
							<div class="h-3 w-3 rounded border border-green-500/50 bg-green-500/30"></div>
							<span class="text-xs text-green-200/80 line-through opacity-60">Bell peppers</span>
						</div>
						<div class="flex items-center gap-2">
							<div class="h-3 w-3 rounded border border-green-500/50"></div>
							<span class="text-xs text-green-200/80">Feta cheese</span>
						</div>
						<div class="flex items-center gap-2">
							<div class="h-3 w-3 rounded border border-green-500/50"></div>
							<span class="text-xs text-green-200/80">Bananas</span>
						</div>
					</div>
				</div>
			</div>
		</div>
	</div>

	<!-- Wave divider -->
	<div class="text-cream">
		<svg
			viewBox="0 0 1440 100"
			fill="currentColor"
			preserveAspectRatio="none"
			class="block h-16 w-full md:h-24"
		>
			<path d="M0,40 C360,100 1080,0 1440,60 L1440,100 L0,100 Z" />
		</svg>
	</div>
</section>

<!-- ═══════════════════════════════════════════════════════ FEATURES -->
<section id="features" class="bg-cream py-20 md:py-28">
	<div class="mx-auto max-w-7xl px-6">
		<div class="reveal-on-scroll mx-auto mb-16 max-w-2xl text-center opacity-0">
			<span
				class="mb-3 inline-block font-display text-sm font-semibold tracking-widest text-green-600 uppercase"
				>Everything you need</span
			>
			<h2 class="mb-4 font-display text-3xl font-bold tracking-tight text-green-900 sm:text-4xl">
				Meal planning, simplified
			</h2>
			<p class="text-lg text-charcoal/70">
				Stop juggling spreadsheets and sticky notes. Simple Meal Planner brings your recipes, calendar, and
				shopping list together.
			</p>
		</div>

		<div class="grid gap-8 md:grid-cols-3">
			<!-- Feature 1 -->
			<div
				class="reveal-on-scroll group rounded-2xl border border-green-200/60 bg-white p-8 opacity-0 shadow-sm transition-all duration-300 hover:-translate-y-1 hover:shadow-lg hover:shadow-green-200/30"
			>
				<div
					class="mb-5 flex h-12 w-12 items-center justify-center rounded-xl bg-green-100 text-green-600 transition-colors group-hover:bg-green-500 group-hover:text-white"
				>
					<svg
						xmlns="http://www.w3.org/2000/svg"
						class="h-6 w-6"
						fill="none"
						viewBox="0 0 24 24"
						stroke="currentColor"
						stroke-width="2"
					>
						<path
							stroke-linecap="round"
							stroke-linejoin="round"
							d="M8 7V3m8 4V3m-9 8h10M5 21h14a2 2 0 0 0 2-2V7a2 2 0 0 0-2-2H5a2 2 0 0 0-2 2v12a2 2 0 0 0 2 2Z"
						/>
					</svg>
				</div>
				<h3 class="mb-2 font-display text-lg font-bold text-green-900">Weekly Meal Calendar</h3>
				<p class="leading-relaxed text-charcoal/70">
					Drag and drop recipes onto your calendar. See your whole week at a glance and keep your
					family on the same page.
				</p>
			</div>

			<!-- Feature 2 -->
			<div
				class="reveal-on-scroll group rounded-2xl border border-green-200/60 bg-white p-8 opacity-0 shadow-sm transition-all duration-300 hover:-translate-y-1 hover:shadow-lg hover:shadow-green-200/30"
			>
				<div
					class="mb-5 flex h-12 w-12 items-center justify-center rounded-xl bg-green-100 text-green-600 transition-colors group-hover:bg-green-500 group-hover:text-white"
				>
					<svg
						xmlns="http://www.w3.org/2000/svg"
						class="h-6 w-6"
						fill="none"
						viewBox="0 0 24 24"
						stroke="currentColor"
						stroke-width="2"
					>
						<path
							stroke-linecap="round"
							stroke-linejoin="round"
							d="M9 5H7a2 2 0 0 0-2 2v12a2 2 0 0 0 2 2h10a2 2 0 0 0 2-2V7a2 2 0 0 0-2-2h-2M9 5a2 2 0 0 0 2 2h2a2 2 0 0 0 2-2M9 5a2 2 0 0 1 2-2h2a2 2 0 0 1 2 2m-6 9 2 2 4-4"
						/>
					</svg>
				</div>
				<h3 class="mb-2 font-display text-lg font-bold text-green-900">Smart Grocery Lists</h3>
				<p class="leading-relaxed text-charcoal/70">
					One click generates a consolidated grocery list from all your planned meals. No more
					forgotten ingredients or duplicate items.
				</p>
			</div>

			<!-- Feature 3 -->
			<div
				class="reveal-on-scroll group rounded-2xl border border-green-200/60 bg-white p-8 opacity-0 shadow-sm transition-all duration-300 hover:-translate-y-1 hover:shadow-lg hover:shadow-green-200/30"
			>
				<div
					class="mb-5 flex h-12 w-12 items-center justify-center rounded-xl bg-green-100 text-green-600 transition-colors group-hover:bg-green-500 group-hover:text-white"
				>
					<svg
						xmlns="http://www.w3.org/2000/svg"
						class="h-6 w-6"
						fill="none"
						viewBox="0 0 24 24"
						stroke="currentColor"
						stroke-width="2"
					>
						<path
							stroke-linecap="round"
							stroke-linejoin="round"
							d="M12 6.253v13m0-13C10.832 5.477 9.246 5 7.5 5S4.168 5.477 3 6.253v13C4.168 18.477 5.754 18 7.5 18s3.332.477 4.5 1.253m0-13C13.168 5.477 14.754 5 16.5 5c1.747 0 3.332.477 4.5 1.253v13C19.832 18.477 18.247 18 16.5 18c-1.746 0-3.332.477-4.5 1.253"
						/>
					</svg>
				</div>
				<h3 class="mb-2 font-display text-lg font-bold text-green-900">Recipe Library</h3>
				<p class="leading-relaxed text-charcoal/70">
					Save links to your favourite recipes from any website. Build a personalised collection you
					can search and reuse week after week.
				</p>
			</div>
		</div>
	</div>
</section>

<!-- ══════════════════════════════════════════════════ HOW IT WORKS -->
<section id="how-it-works" class="grain relative bg-green-50 py-20 md:py-28">
	<div class="relative mx-auto max-w-7xl px-6">
		<div class="reveal-on-scroll mx-auto mb-16 max-w-2xl text-center opacity-0">
			<span
				class="mb-3 inline-block font-display text-sm font-semibold tracking-widest text-green-600 uppercase"
				>Three simple steps</span
			>
			<h2 class="mb-4 font-display text-3xl font-bold tracking-tight text-green-900 sm:text-4xl">
				How it works
			</h2>
			<p class="text-lg text-charcoal/70">From recipe to shopping cart in under a minute.</p>
		</div>

		<div class="grid gap-12 md:grid-cols-3">
			<!-- Step 1 -->
			<div class="reveal-on-scroll text-center opacity-0">
				<div
					class="mx-auto mb-6 flex h-16 w-16 items-center justify-center rounded-2xl bg-green-500 text-2xl font-bold text-white shadow-lg shadow-green-500/30"
				>
					<span class="font-display">1</span>
				</div>
				<h3 class="mb-2 font-display text-lg font-bold text-green-900">Add Recipes</h3>
				<p class="leading-relaxed text-charcoal/70">
					Paste a link or enter ingredients manually. Your recipe library grows with every meal you
					plan.
				</p>
			</div>

			<!-- Step 2 -->
			<div class="reveal-on-scroll text-center opacity-0">
				<div
					class="mx-auto mb-6 flex h-16 w-16 items-center justify-center rounded-2xl bg-green-500 text-2xl font-bold text-white shadow-lg shadow-green-500/30"
				>
					<span class="font-display">2</span>
				</div>
				<h3 class="mb-2 font-display text-lg font-bold text-green-900">Plan Your Week</h3>
				<p class="leading-relaxed text-charcoal/70">
					Drag recipes onto your weekly calendar. Mix and match across breakfast, lunch, and dinner
					slots.
				</p>
			</div>

			<!-- Step 3 -->
			<div class="reveal-on-scroll text-center opacity-0">
				<div
					class="mx-auto mb-6 flex h-16 w-16 items-center justify-center rounded-2xl bg-green-500 text-2xl font-bold text-white shadow-lg shadow-green-500/30"
				>
					<span class="font-display">3</span>
				</div>
				<h3 class="mb-2 font-display text-lg font-bold text-green-900">Generate Grocery List</h3>
				<p class="leading-relaxed text-charcoal/70">
					Hit one button and get a combined, de-duplicated shopping list. Check items off as you
					shop.
				</p>
			</div>
		</div>
	</div>
</section>

<!-- ═══════════════════════════════════════════════ TESTIMONIALS -->
<section id="testimonials" class="bg-cream py-20 md:py-28">
	<div class="mx-auto max-w-7xl px-6">
		<div class="reveal-on-scroll mx-auto mb-16 max-w-2xl text-center opacity-0">
			<span
				class="mb-3 inline-block font-display text-sm font-semibold tracking-widest text-green-600 uppercase"
				>What people say</span
			>
			<h2 class="mb-4 font-display text-3xl font-bold tracking-tight text-green-900 sm:text-4xl">
				Loved by home cooks
			</h2>
		</div>

		<div class="grid gap-8 md:grid-cols-3">
			{#each [{ quote: 'I used to spend 30 minutes every Sunday just figuring out what to cook. Now it takes me five minutes and I actually look forward to it.', name: 'Sarah M.', role: 'Busy parent of two', stars: 5 }, { quote: 'The grocery list feature alone is worth it. No more wandering aimlessly through the store or forgetting half the ingredients.', name: 'James K.', role: 'Meal-prep enthusiast', stars: 5 }, { quote: "So simple, so elegant. I've tried a dozen meal planning apps and this is the first one that actually stuck.", name: 'Priya L.', role: 'Home cook', stars: 5 }] as testimonial}
				<div
					class="reveal-on-scroll rounded-2xl border border-green-200/60 bg-white p-8 opacity-0 shadow-sm"
				>
					<!-- Stars -->
					<div class="mb-4 flex gap-1">
						{#each Array(testimonial.stars) as _}
							<svg
								xmlns="http://www.w3.org/2000/svg"
								class="h-4 w-4 text-yellow-400"
								fill="currentColor"
								viewBox="0 0 24 24"
							>
								<path
									d="M12 2l3.09 6.26L22 9.27l-5 4.87 1.18 6.88L12 17.77l-6.18 3.25L7 14.14 2 9.27l6.91-1.01L12 2z"
								/>
							</svg>
						{/each}
					</div>
					<p class="mb-6 leading-relaxed text-charcoal/80 italic">
						"{testimonial.quote}"
					</p>
					<div>
						<span class="block font-display text-sm font-bold text-green-900"
							>{testimonial.name}</span
						>
						<span class="text-xs text-charcoal/50">{testimonial.role}</span>
					</div>
				</div>
			{/each}
		</div>
	</div>
</section>

<!-- ════════════════════════════════════════════════════════ CTA -->
<section
	class="grain relative overflow-hidden bg-gradient-to-br from-green-800 via-green-700 to-green-600 py-20 md:py-28"
>
	<div class="absolute -top-20 -right-20 h-80 w-80 rounded-full bg-green-500/10 blur-3xl"></div>
	<div class="absolute -bottom-20 -left-20 h-64 w-64 rounded-full bg-green-400/10 blur-3xl"></div>

	<div class="reveal-on-scroll relative mx-auto max-w-3xl px-6 text-center opacity-0">
		<h2
			class="mb-6 font-display text-3xl font-bold tracking-tight text-white sm:text-4xl lg:text-5xl"
		>
			Ready to take the stress out of dinner?
		</h2>
		<p class="mx-auto mb-10 max-w-xl text-lg text-green-100/80">
			Join thousands of home cooks who plan smarter, shop faster, and eat better — starting tonight.
		</p>
		<a
			href="/signup"
			class="inline-block rounded-xl bg-white px-8 py-4 font-display text-sm font-bold text-green-800 shadow-lg shadow-green-900/30 transition-all hover:-translate-y-0.5 hover:shadow-xl"
		>
			Start Planning — It's Free
		</a>
		<p class="mt-4 font-display text-xs text-green-300/60">
			No credit card required · Cancel anytime
		</p>
	</div>
</section>

<!-- ═══════════════════════════════════════════════════════ FOOTER -->
<footer class="border-t border-green-200/40 bg-green-900 py-12">
	<div class="mx-auto max-w-7xl px-6">
		<div class="grid gap-10 md:grid-cols-4">
			<!-- Brand -->
			<div class="md:col-span-1">
				<div class="mb-4 flex items-center gap-2.5">
					<div class="flex h-8 w-8 items-center justify-center rounded-lg bg-green-500">
						<svg
							xmlns="http://www.w3.org/2000/svg"
							viewBox="0 0 24 24"
							fill="none"
							stroke="currentColor"
							stroke-width="2"
							stroke-linecap="round"
							stroke-linejoin="round"
							class="h-4 w-4 text-white"
						>
							<path
								d="M17 8c.7-1 1-2.2 1-3.5C18 2.5 16.5 1 14.5 1S11 2.5 11 4.5c0 1.3.3 2.5 1 3.5"
							/>
							<path d="M7 8c-.7-1-1-2.2-1-3.5C6 2.5 7.5 1 9.5 1S13 2.5 13 4.5c0 1.3-.3 2.5-1 3.5" />
							<path d="M12 8a5 5 0 0 0-4.8 3.6L5 20h14l-2.2-8.4A5 5 0 0 0 12 8Z" />
							<path d="M12 8v2" />
						</svg>
					</div>
					<span class="font-display text-lg font-bold text-white">Simple Meal Planner</span>
				</div>
				<p class="text-sm leading-relaxed text-green-300/60">
					Making meal planning effortless for families and home cooks everywhere.
				</p>
			</div>

			<!-- Quick Links -->
			<div>
				<h4
					class="mb-4 font-display text-xs font-semibold tracking-widest text-green-400 uppercase"
				>
					Product
				</h4>
				<ul class="space-y-2.5">
					<li>
						<a href="#features" class="text-sm text-green-200/70 transition-colors hover:text-white"
							>Features</a
						>
					</li>
					<li>
						<a
							href="#how-it-works"
							class="text-sm text-green-200/70 transition-colors hover:text-white">How It Works</a
						>
					</li>
					<li>
						<a href="/pricing" class="text-sm text-green-200/70 transition-colors hover:text-white"
							>Pricing</a
						>
					</li>
				</ul>
			</div>

			<div>
				<h4
					class="mb-4 font-display text-xs font-semibold tracking-widest text-green-400 uppercase"
				>
					Company
				</h4>
				<ul class="space-y-2.5">
					<li>
						<a href="/about" class="text-sm text-green-200/70 transition-colors hover:text-white"
							>About</a
						>
					</li>
					<li>
						<a href="/blog" class="text-sm text-green-200/70 transition-colors hover:text-white"
							>Blog</a
						>
					</li>
					<li>
						<a href="/contact" class="text-sm text-green-200/70 transition-colors hover:text-white"
							>Contact</a
						>
					</li>
				</ul>
			</div>

			<div>
				<h4
					class="mb-4 font-display text-xs font-semibold tracking-widest text-green-400 uppercase"
				>
					Legal
				</h4>
				<ul class="space-y-2.5">
					<li>
						<a href="/privacy" class="text-sm text-green-200/70 transition-colors hover:text-white"
							>Privacy Policy</a
						>
					</li>
					<li>
						<a href="/terms" class="text-sm text-green-200/70 transition-colors hover:text-white"
							>Terms of Service</a
						>
					</li>
				</ul>
			</div>
		</div>

		<div
			class="mt-10 flex flex-col items-center justify-between gap-4 border-t border-green-700/40 pt-8 md:flex-row"
		>
			<p class="font-display text-xs text-green-400/50">
				© {new Date().getFullYear()} Simple Meal Planner. All rights reserved.
			</p>
			<div class="flex gap-4">
				<!-- Twitter / X -->
				<a
					href="https://x.com"
					class="text-green-400/40 transition-colors hover:text-green-300"
					aria-label="Twitter"
					target="_blank"
					rel="noopener noreferrer"
				>
					<svg
						xmlns="http://www.w3.org/2000/svg"
						class="h-5 w-5"
						fill="currentColor"
						viewBox="0 0 24 24"
					>
						<path
							d="M18.244 2.25h3.308l-7.227 8.26 8.502 11.24H16.17l-5.214-6.817L4.99 21.75H1.68l7.73-8.835L1.254 2.25H8.08l4.713 6.231zm-1.161 17.52h1.833L7.084 4.126H5.117z"
						/>
					</svg>
				</a>
				<!-- GitHub -->
				<a
					href="https://github.com"
					class="text-green-400/40 transition-colors hover:text-green-300"
					aria-label="GitHub"
					target="_blank"
					rel="noopener noreferrer"
				>
					<svg
						xmlns="http://www.w3.org/2000/svg"
						class="h-5 w-5"
						fill="currentColor"
						viewBox="0 0 24 24"
					>
						<path
							d="M12 0c-6.626 0-12 5.373-12 12 0 5.302 3.438 9.8 8.207 11.387.599.111.793-.261.793-.577v-2.234c-3.338.726-4.033-1.416-4.033-1.416-.546-1.387-1.333-1.756-1.333-1.756-1.089-.745.083-.729.083-.729 1.205.084 1.839 1.237 1.839 1.237 1.07 1.834 2.807 1.304 3.492.997.107-.775.418-1.305.762-1.604-2.665-.305-5.467-1.334-5.467-5.931 0-1.311.469-2.381 1.236-3.221-.124-.303-.535-1.524.117-3.176 0 0 1.008-.322 3.301 1.23.957-.266 1.983-.399 3.003-.404 1.02.005 2.047.138 3.006.404 2.291-1.552 3.297-1.23 3.297-1.23.653 1.653.242 2.874.118 3.176.77.84 1.235 1.911 1.235 3.221 0 4.609-2.807 5.624-5.479 5.921.43.372.823 1.102.823 2.222v3.293c0 .319.192.694.801.576 4.765-1.589 8.199-6.086 8.199-11.386 0-6.627-5.373-12-12-12z"
						/>
					</svg>
				</a>
			</div>
		</div>
	</div>
</footer>
