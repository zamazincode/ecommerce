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
	const response = await fetch(`/api/backend/${path}`, {
		...init,
		headers: {
			"Content-Type": "application/json",
			...init?.headers,
		},
	});

	const body = await response.json().catch(() => null);

	if (!response.ok) {
		throw new ApiError(response.status, body);
	}

	return body as T;
}
