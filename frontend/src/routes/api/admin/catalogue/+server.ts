import type { RequestHandler } from './$types';
import { proxyAdminCatalogueRequest } from './proxy';

const forward: RequestHandler = (event) => proxyAdminCatalogueRequest(event);

export const GET: RequestHandler = forward;
export const POST: RequestHandler = forward;
export const PUT: RequestHandler = forward;
export const PATCH: RequestHandler = forward;
export const DELETE: RequestHandler = forward;
