import { describe, expect, it } from 'vitest';
import {
	buildTargeting,
	parseTargeting,
	type TargetingModel,
	type TargetingRule
} from './targeting';

const rule = (overrides: Partial<TargetingRule> = {}): TargetingRule => ({
	attribute: 'role',
	operator: '==',
	value: 'admin',
	variant: 'on',
	...overrides
});

describe('buildTargeting', () => {
	it('returns null when there is nothing to target on', () => {
		expect(buildTargeting({ rules: [], rollout: null })).toBeNull();
	});

	it('ignores rules with a blank variant or value', () => {
		const model: TargetingModel = {
			rules: [rule({ variant: '  ' }), rule({ value: '' })],
			rollout: null
		};

		expect(buildTargeting(model)).toBeNull();
	});

	it('builds an if chain returning variant names', () => {
		const targeting = buildTargeting({ rules: [rule()], rollout: null });

		expect(targeting).toEqual({
			if: [{ '==': [{ var: 'role' }, 'admin'] }, 'on']
		});
	});

	it('splits a comma-separated list for the in operator', () => {
		const targeting = buildTargeting({
			rules: [rule({ attribute: 'email', operator: 'in', value: 'a@x.com, b@x.com , ' })],
			rollout: null
		});

		expect(targeting).toEqual({
			if: [{ in: [{ var: 'email' }, ['a@x.com', 'b@x.com']] }, 'on']
		});
	});

	it('emits a bare fractional block for a rollout with no rules', () => {
		const targeting = buildTargeting({
			rules: [],
			rollout: { variant: 'on', percentage: 25, fallbackVariant: 'off' }
		});

		expect(targeting).toEqual({
			fractional: [{ var: 'targetingKey' }, ['on', 25], ['off', 75]]
		});
	});

	it('appends the rollout as the else branch so explicit rules win', () => {
		const targeting = buildTargeting({
			rules: [rule()],
			rollout: { variant: 'on', percentage: 10, fallbackVariant: 'off' }
		});

		expect(targeting).toEqual({
			if: [
				{ '==': [{ var: 'role' }, 'admin'] },
				'on',
				{ fractional: [{ var: 'targetingKey' }, ['on', 10], ['off', 90]] }
			]
		});
	});
});

describe('parseTargeting', () => {
	it('treats an absent block as an empty model', () => {
		expect(parseTargeting(undefined)).toEqual({ rules: [], rollout: null });
		expect(parseTargeting(null)).toEqual({ rules: [], rollout: null });
	});

	it.each([
		['a rule chain', { rules: [rule()], rollout: null }],
		[
			'a list membership rule',
			{
				rules: [rule({ attribute: 'email', operator: 'in', value: 'a@x.com, b@x.com' })],
				rollout: null
			}
		],
		[
			'a bare rollout',
			{ rules: [], rollout: { variant: 'on', percentage: 25, fallbackVariant: 'off' } }
		],
		[
			'rules plus a rollout',
			{
				rules: [rule(), rule({ attribute: 'email', operator: 'starts_with', value: 'qa+' })],
				rollout: { variant: 'on', percentage: 5, fallbackVariant: 'off' }
			}
		]
	])('round-trips %s', (_label, model) => {
		expect(parseTargeting(buildTargeting(model as TargetingModel))).toEqual(model);
	});

	it.each([
		['a non-object', 'nope'],
		['an unsupported operator', { if: [{ '>': [{ var: 'role' }, 1] }, 'on'] }],
		['an unknown attribute', { if: [{ '==': [{ var: 'plan' }, 'pro'] }, 'on'] }],
		['a variant that is not a string', { if: [{ '==': [{ var: 'role' }, 'admin'] }, 42] }],
		['a hand-written top-level operator', { and: [true, false] }],
		[
			'a fractional block bucketed on something else',
			{ fractional: [{ var: 'email' }, ['on', 50], ['off', 50]] }
		],
		[
			'fractional weights that do not total 100',
			{ fractional: [{ var: 'targetingKey' }, ['on', 10], ['off', 10]] }
		],
		[
			'more than two fractional buckets',
			{ fractional: [{ var: 'targetingKey' }, ['a', 33], ['b', 33], ['c', 34]] }
		]
	])('returns null for %s so the editor falls back to raw JSON', (_label, targeting) => {
		expect(parseTargeting(targeting)).toBeNull();
	});
});
