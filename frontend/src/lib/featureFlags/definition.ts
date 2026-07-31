import type { FeatureFlagValueType } from '$lib/api/featureFlagsApi';

/**
 * A single variant row in the editor. Values are held as JSON text so a
 * half-typed object or number never destroys what the admin has entered; they
 * are parsed only when the definition is assembled.
 */
export interface VariantDraft {
	name: string;
	valueJson: string;
}

export interface FeatureFlagDefinition {
	variants: Record<string, unknown>;
	defaultVariant: string;
	targeting?: unknown;
}

/**
 * Parses a stored definition into editor state. Returns empty variants rather
 * than throwing so a legacy or hand-edited row still opens in the editor.
 */
export function parseDefinition(definitionJson: string): {
	variants: VariantDraft[];
	defaultVariant: string;
	targetingJson: string;
} {
	let parsed: FeatureFlagDefinition | null = null;

	try {
		const candidate = JSON.parse(definitionJson);
		if (candidate && typeof candidate === 'object' && !Array.isArray(candidate)) {
			parsed = candidate as FeatureFlagDefinition;
		}
	} catch {
		parsed = null;
	}

	const rawVariants =
		parsed?.variants && typeof parsed.variants === 'object' && !Array.isArray(parsed.variants)
			? parsed.variants
			: {};

	return {
		variants: Object.entries(rawVariants).map(([name, value]) => ({
			name,
			valueJson: JSON.stringify(value)
		})),
		defaultVariant: typeof parsed?.defaultVariant === 'string' ? parsed.defaultVariant : '',
		targetingJson: parsed?.targeting === undefined ? '' : JSON.stringify(parsed.targeting, null, 2)
	};
}

/**
 * Reads a variant's JSON text as a value of the declared type. Returns an error
 * message instead of throwing so the editor can point at the offending row.
 */
export function parseVariantValue(
	valueJson: string,
	valueType: FeatureFlagValueType
): { value: unknown } | { error: string } {
	const trimmed = valueJson.trim();
	if (trimmed === '') {
		return { error: 'A value is required.' };
	}

	let value: unknown;
	try {
		value = JSON.parse(trimmed);
	} catch {
		return {
			error:
				valueType === 'string'
					? 'Enter a value in double quotes, for example "beta".'
					: 'Enter a valid JSON value.'
		};
	}

	switch (valueType) {
		case 'boolean':
			return typeof value === 'boolean' ? { value } : { error: 'Expected true or false.' };
		case 'string':
			return typeof value === 'string' ? { value } : { error: 'Expected a string.' };
		case 'number':
			return typeof value === 'number' && Number.isFinite(value)
				? { value }
				: { error: 'Expected a number.' };
		case 'object':
			return value !== null && typeof value === 'object'
				? { value }
				: { error: 'Expected a JSON object or array.' };
	}
}

/**
 * Assembles editor state into the flagd body the API stores, mirroring the
 * server-side checks in FeatureFlagDefinitionValidator so the admin sees
 * problems before a round trip.
 */
export function buildDefinition(input: {
	variants: VariantDraft[];
	defaultVariant: string;
	disabledVariant: string;
	targetingJson: string;
	valueType: FeatureFlagValueType;
}): { definitionJson: string } | { error: string } {
	const named = input.variants.filter((variant) => variant.name.trim() !== '');
	if (named.length === 0) {
		return { error: 'Add at least one variant.' };
	}

	const variants: Record<string, unknown> = {};
	for (const variant of named) {
		const name = variant.name.trim();
		if (name in variants) {
			return { error: `Variant '${name}' is defined more than once.` };
		}

		const parsed = parseVariantValue(variant.valueJson, input.valueType);
		if ('error' in parsed) {
			return { error: `Variant '${name}': ${parsed.error}` };
		}

		variants[name] = parsed.value;
	}

	if (!input.defaultVariant) {
		return { error: 'Choose the variant to serve while the flag is on.' };
	}

	if (!(input.defaultVariant in variants)) {
		return { error: `The default variant '${input.defaultVariant}' is not one of the variants.` };
	}

	if (input.disabledVariant && !(input.disabledVariant in variants)) {
		return { error: `The off variant '${input.disabledVariant}' is not one of the variants.` };
	}

	const definition: FeatureFlagDefinition = {
		variants,
		defaultVariant: input.defaultVariant
	};

	const targeting = input.targetingJson.trim();
	if (targeting !== '') {
		let parsedTargeting: unknown;
		try {
			parsedTargeting = JSON.parse(targeting);
		} catch {
			return { error: 'Targeting rules must be valid JSON.' };
		}

		if (parsedTargeting === null || typeof parsedTargeting !== 'object' || Array.isArray(parsedTargeting)) {
			return { error: 'Targeting rules must be a JSON object.' };
		}

		definition.targeting = parsedTargeting;
	}

	return { definitionJson: JSON.stringify(definition) };
}

/**
 * The value a flag resolves to in each state, used to show admins the effect of
 * the toggle without reading the JSON. Returns null for a variant the flag does
 * not define — including the "no off variant" case, where callers fall back to
 * their own code default.
 */
export function describeResolvedValues(flag: {
	definitionJson: string;
	disabledVariant: string | null;
}): { onValue: string | null; offValue: string | null } {
	const { variants, defaultVariant } = parseDefinition(flag.definitionJson);
	const lookup = new Map(variants.map((variant) => [variant.name, variant.valueJson]));

	return {
		onValue: lookup.get(defaultVariant) ?? null,
		offValue: flag.disabledVariant ? (lookup.get(flag.disabledVariant) ?? null) : null
	};
}
