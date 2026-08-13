import { getOrCreateGuestId } from "@/lib/guest-id";

export class ApiError extends Error {
	constructor(
		public status: number,
		public body: unknown,
	) {
		super(`API hatası: ${status}`);
	}
}

export async function apiFetch<T>(
	path: string,
	init?: RequestInit,
): Promise<T> {
	const isCartRequest = path.startsWith("cart");

	const response = await fetch(`/api/backend/${path}`, {
		...init,
		headers: {
			"Content-Type": "application/json",
			// Sadece sepet isteklerinde — diğer endpoint'lere gereksiz
			// header taşımaya gerek yok.
			...(isCartRequest ? { "X-Guest-Id": getOrCreateGuestId() } : {}),
			...init?.headers,
		},
	});

	const body = await response.json().catch(() => null);

	if (!response.ok) {
		throw new ApiError(response.status, body);
	}

	return body as T;
}
