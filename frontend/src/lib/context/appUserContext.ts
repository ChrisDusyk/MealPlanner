import type { AppUserResponse } from '$lib/api/userApi';

export type AppUser = AppUserResponse;

export interface AppUserState {
	current: AppUser | null;
}

export interface AppUserContextValue {
	appUserState: AppUserState;
	setAppUser: (user: AppUser | null) => void;
}

export const APP_USER_CONTEXT_KEY = Symbol('app-user-context');
