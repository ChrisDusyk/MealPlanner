import type { RequestHandler } from './$types';
import { proxyAdminCatalogueRequest } from '../proxy';

export const POST: RequestHandler = (event) =>
	proxyAdminCatalogueRequest(event, 'import-from-url');
