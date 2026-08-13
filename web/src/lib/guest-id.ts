const GUEST_ID_COOKIE = "commerce_guest_id";
const GUEST_ID_MAX_AGE = 60 * 60 * 24 * 30; // Redis TTL'i ile aynı

/** Client Component'lerden çağrılır. Cookie yoksa üretir, varsa okur. */
export function getOrCreateGuestId(): string {
	const existing = readCookie(GUEST_ID_COOKIE);
	if (existing) return existing;

	const id = crypto.randomUUID();
	document.cookie = `${GUEST_ID_COOKIE}=${id}; path=/; max-age=${GUEST_ID_MAX_AGE}; samesite=lax`;
	return id;
}

export function clearGuestId() {
	document.cookie = `${GUEST_ID_COOKIE}=; path=/; max-age=0`;
}

function readCookie(name: string): string | null {
	const match = document.cookie.match(new RegExp(`(?:^|; )${name}=([^;]*)`));
	return match ? decodeURIComponent(match[1]) : null;
}
