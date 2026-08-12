import { ResponseCookie } from "next/dist/compiled/@edge-runtime/cookies";
import { cookies } from "next/headers";
import "server-only";

const ACCESS_TOKEN_COOKIE = "commerce_at";
const REFRESH_TOKEN_COOKIE = "commerce_rt";

const ACCESS_TOKEN_MAX_AGE = 15 * 60;
const REFRESH_TOKEN_MAX_AGE = 30 * 24 * 60 * 60;

export interface TokenPair {
	accessToken: string;
	refreshToken: string;
}

export async function setSessionCookies({
	accessToken,
	refreshToken,
}: TokenPair) {
	const cookieStore = await cookies();

	const common: Partial<ResponseCookie> = {
		httpOnly: true,
		// productionda değilse http olsun
		secure: process.env.NODE_ENV === "production",
		sameSite: "lax",
		path: "/",
	};

	cookieStore.set(ACCESS_TOKEN_COOKIE, accessToken, {
		...common,
		maxAge: ACCESS_TOKEN_MAX_AGE,
	});

	cookieStore.set(REFRESH_TOKEN_COOKIE, refreshToken, {
		...common,
		maxAge: REFRESH_TOKEN_MAX_AGE,
	});
}

export async function clearSessionCookies() {
	const cookieStore = await cookies();

	cookieStore.delete(ACCESS_TOKEN_COOKIE);
	cookieStore.delete(REFRESH_TOKEN_COOKIE);
}

export async function getAccessToken(): Promise<string | undefined> {
	const cookieStore = await cookies();
	return cookieStore.get(ACCESS_TOKEN_COOKIE)?.value;
}

export async function getRefreshToken(): Promise<string | undefined> {
	const cookieStore = await cookies();
	return cookieStore.get(REFRESH_TOKEN_COOKIE)?.value;
}
