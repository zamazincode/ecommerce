import "server-only";
import { getAccessToken } from "./session";

const API_BASE = process.env.API_INTERNAL_URL;

export async function serverApiFetch<T>(
	path: string,
	init?: RequestInit & {
		next?: { revalidate?: number | false; tags?: string[] };
	},
): Promise<T | null> {
	const accessToken = await getAccessToken();

	const response = await fetch(`${API_BASE}/api/${path}`, {
		...init,
		headers: {
			"Content-Type": "application/json",
			...(accessToken ? { Authorization: `Bearer ${accessToken}` } : {}),
			...init?.headers,
		},
	});

	if (response.status === 401) {
		return null;
	}

	if (!response.ok) return null;
	return (await response.json()) as T;
}
