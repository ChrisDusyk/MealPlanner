export type ToastType = 'success' | 'error';

// Module-level runes state shared by every consumer. Only mutate from browser
// event handlers — never call toast.show() during load functions or SSR, as the
// state is shared per server process.
let message = $state('');
let type: ToastType = $state('success');
let visible = $state(false);

let timer: ReturnType<typeof setTimeout> | null = null;

function show(newMessage: string, newType: ToastType = 'success', duration = 3000) {
	// Clear any pending hide so a new toast always gets its full duration.
	if (timer) clearTimeout(timer);

	message = newMessage;
	type = newType;
	visible = true;

	timer = setTimeout(() => {
		visible = false;
		timer = null;
	}, duration);
}

function dismiss() {
	if (timer) {
		clearTimeout(timer);
		timer = null;
	}
	visible = false;
}

export const toast = {
	get message() {
		return message;
	},
	get type() {
		return type;
	},
	get visible() {
		return visible;
	},
	show,
	success: (msg: string, duration?: number) => show(msg, 'success', duration),
	error: (msg: string, duration?: number) => show(msg, 'error', duration),
	dismiss
};
