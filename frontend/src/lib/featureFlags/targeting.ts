/**
 * Round-trips between flagd's JsonLogic targeting and the structured rows the
 * admin editor shows. The builder deliberately covers a narrow slice of
 * JsonLogic; anything it cannot represent parses back as `null`, which is the
 * editor's cue to fall back to raw JSON editing rather than silently rewriting
 * rules an admin wrote by hand.
 */

/** Context attributes the API populates. Widening this needs matching API work. */
export const TARGETING_ATTRIBUTES = ['targetingKey', 'email', 'role'] as const;

export type TargetingAttribute = (typeof TARGETING_ATTRIBUTES)[number];

export const TARGETING_OPERATORS = ['==', '!=', 'starts_with', 'ends_with', 'in'] as const;

export type TargetingOperator = (typeof TARGETING_OPERATORS)[number];

export const TARGETING_OPERATOR_LABELS: Record<TargetingOperator, string> = {
	'==': 'is',
	'!=': 'is not',
	starts_with: 'starts with',
	ends_with: 'ends with',
	in: 'is one of'
};

export interface TargetingRule {
	attribute: TargetingAttribute;
	operator: TargetingOperator;
	/** Comma-separated for the `in` operator, a single value otherwise. */
	value: string;
	variant: string;
}

export interface PercentageRollout {
	variant: string;
	/** Whole percent, 1–99. The remainder falls through to the default variant. */
	percentage: number;
	/** Variant serving the remaining share. */
	fallbackVariant: string;
}

export interface TargetingModel {
	rules: TargetingRule[];
	rollout: PercentageRollout | null;
}

const isRecord = (value: unknown): value is Record<string, unknown> =>
	value !== null && typeof value === 'object' && !Array.isArray(value);

const isVar = (value: unknown): value is { var: string } =>
	isRecord(value) && typeof value.var === 'string' && Object.keys(value).length === 1;

function isAttribute(name: string): name is TargetingAttribute {
	return (TARGETING_ATTRIBUTES as readonly string[]).includes(name);
}

function isOperator(name: string): name is TargetingOperator {
	return (TARGETING_OPERATORS as readonly string[]).includes(name);
}

/**
 * Turns structured rows into a JsonLogic block, or null when there is nothing
 * to target on.
 *
 * Rules become an if/else-if chain returning variant names, evaluated top to
 * bottom. A percentage rollout is the final fallback, so explicit rules always
 * win over the rollout bucket.
 */
export function buildTargeting(model: TargetingModel): unknown | null {
	const usableRules = model.rules.filter(
		(rule) => rule.variant.trim() !== '' && rule.value.trim() !== ''
	);

	const rollout =
		model.rollout && model.rollout.variant.trim() !== '' && model.rollout.fallbackVariant.trim() !== ''
			? model.rollout
			: null;

	if (usableRules.length === 0 && !rollout) {
		return null;
	}

	const branches: unknown[] = [];

	for (const rule of usableRules) {
		branches.push(buildCondition(rule), rule.variant.trim());
	}

	const fallback = rollout
		? {
				fractional: [
					{ var: 'targetingKey' },
					[rollout.variant.trim(), rollout.percentage],
					[rollout.fallbackVariant.trim(), 100 - rollout.percentage]
				]
			}
		: null;

	if (branches.length === 0) {
		return fallback;
	}

	if (fallback !== null) {
		branches.push(fallback);
	}

	return { if: branches };
}

function buildCondition(rule: TargetingRule): unknown {
	const attribute = { var: rule.attribute };

	if (rule.operator === 'in') {
		const values = rule.value
			.split(',')
			.map((entry) => entry.trim())
			.filter((entry) => entry !== '');
		return { in: [attribute, values] };
	}

	return { [rule.operator]: [attribute, rule.value.trim()] };
}

/**
 * Reads a JsonLogic block back into structured rows, or returns null when the
 * block uses anything the builder cannot express.
 */
export function parseTargeting(targeting: unknown): TargetingModel | null {
	if (targeting === null || targeting === undefined) {
		return { rules: [], rollout: null };
	}

	if (!isRecord(targeting)) {
		return null;
	}

	if ('fractional' in targeting) {
		const rollout = parseRollout(targeting);
		return rollout ? { rules: [], rollout } : null;
	}

	if (!('if' in targeting) || !Array.isArray(targeting.if)) {
		return null;
	}

	const branches = targeting.if;
	const rules: TargetingRule[] = [];
	let rollout: PercentageRollout | null = null;

	for (let index = 0; index + 1 < branches.length; index += 2) {
		const rule = parseCondition(branches[index]);
		const variant = branches[index + 1];

		if (rule === null || typeof variant !== 'string') {
			return null;
		}

		rules.push({ ...rule, variant });
	}

	// An odd length means a trailing else branch, which the builder only ever
	// emits as a percentage rollout.
	if (branches.length % 2 === 1) {
		const trailing = branches[branches.length - 1];
		if (!isRecord(trailing)) {
			return null;
		}

		rollout = parseRollout(trailing);
		if (rollout === null) {
			return null;
		}
	}

	return { rules, rollout };
}

function parseCondition(condition: unknown): Omit<TargetingRule, 'variant'> | null {
	if (!isRecord(condition)) {
		return null;
	}

	const entries = Object.entries(condition);
	if (entries.length !== 1) {
		return null;
	}

	const [operator, operands] = entries[0];
	if (!isOperator(operator) || !Array.isArray(operands) || operands.length !== 2) {
		return null;
	}

	const [left, right] = operands;
	if (!isVar(left) || !isAttribute(left.var)) {
		return null;
	}

	if (operator === 'in') {
		if (!Array.isArray(right) || right.some((entry) => typeof entry !== 'string')) {
			return null;
		}

		return { attribute: left.var, operator, value: (right as string[]).join(', ') };
	}

	if (typeof right !== 'string') {
		return null;
	}

	return { attribute: left.var, operator, value: right };
}

function parseRollout(node: Record<string, unknown>): PercentageRollout | null {
	const buckets = node.fractional;
	if (Object.keys(node).length !== 1 || !Array.isArray(buckets)) {
		return null;
	}

	// The builder always emits a targetingKey bucketing expression followed by
	// exactly two weighted buckets.
	const [bucketBy, first, second, ...rest] = buckets;
	if (rest.length > 0 || !isVar(bucketBy) || bucketBy.var !== 'targetingKey') {
		return null;
	}

	const parsedFirst = parseBucket(first);
	const parsedSecond = parseBucket(second);
	if (!parsedFirst || !parsedSecond || parsedFirst.weight + parsedSecond.weight !== 100) {
		return null;
	}

	return {
		variant: parsedFirst.variant,
		percentage: parsedFirst.weight,
		fallbackVariant: parsedSecond.variant
	};
}

function parseBucket(bucket: unknown): { variant: string; weight: number } | null {
	if (!Array.isArray(bucket) || bucket.length !== 2) {
		return null;
	}

	const [variant, weight] = bucket;
	if (typeof variant !== 'string' || typeof weight !== 'number' || !Number.isInteger(weight)) {
		return null;
	}

	return { variant, weight };
}
