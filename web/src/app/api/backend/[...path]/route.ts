/*
	Token ihtiyacı olan routelar önce buradan geçecek, token kontrolü yapılacak.
	Uygunsa backende istek atılacak.
*/

import { NextResponse, type NextRequest } from "next/server";
import {
	getAccessToken,
	getRefreshToken,
	setSessionCookies,
	clearSessionCookies,
} from "@/lib/api/session";

const API_BASE = process.env.API_INTERNAL_URL;

// Not: Bu, "tek bir Node sürecinde paylaşılan sıradan bir JS değişkeni"
// olduğu için çalışıyor — Next.js dev sunucusu ve `next start` (Node
// runtime) bu varsayımla uyumlu. İleride Edge runtime'a ya da çoklu-instance
// (birden fazla sunucu kopyası) bir deploy'a geçilirse (Faz D2), her kopyanın
// kendi ayrı belleği olacağından bu değişken paylaşılmaz olur — o zaman
// Redis gibi paylaşılan bir depoya taşınması gerekir. Şimdilik gerekmiyor.
let refreshPromise: Promise<string | null> | null = null;

async function refreshAccessToken(): Promise<string | null> {
	if (refreshPromise) return refreshPromise;

	refreshPromise = (async () => {
		const refreshToken = await getRefreshToken();
		if (!refreshToken) return null;

		const response = await fetch(`${API_BASE}/api/auth/refresh`, {
			method: "POST",
			headers: { "Content-Type": "application/json" },
			body: JSON.stringify({ refreshToken }),
			cache: "no-store",
		});

		if (!response.ok) {
			await clearSessionCookies();
			return null;
		}

		const auth = await response.json();
		await setSessionCookies({
			accessToken: auth.accessToken,
			refreshToken: auth.refreshToken,
		});
		return auth.accessToken as string;
	})();

	try {
		return await refreshPromise;
	} finally {
		refreshPromise = null;
	}
}

async function forward(request: NextRequest, path: string[]) {
	const accessToken = await getAccessToken();

	const targetUrl = `${API_BASE}/api/${path.join("/")}${request.nextUrl.search}`;

	const doFetch = async (token: string | undefined) =>
		fetch(targetUrl, {
			method: request.method,
			headers: {
				"Content-Type":
					request.headers.get("content-type") ?? "application/json",
				...(token ? { Authorization: `Bearer ${token}` } : {}),
				// YENİ: misafir sepeti .NET tarafında bu başlıktan çözülüyor
				// (CartOwner.ParseGuestId). Giriş yapılmışsa .NET zaten UserId'yi
				// önceliklendiriyor, bu başlık zararsızca yok sayılıyor —
				// koşulsuz iletmek güvenli.
				...(request.headers.get("x-guest-id")
					? { "X-Guest-Id": request.headers.get("x-guest-id")! }
					: {}),
			},
			body: ["GET", "HEAD"].includes(request.method)
				? undefined
				: await request.text(),
			cache: "no-store",
		});

	let response = await doFetch(accessToken);

	// 401 unauthorized
	if (response.status === 401 && accessToken) {
		const newAccessToken = await refreshAccessToken();
		if (newAccessToken) {
			response = await doFetch(newAccessToken);
		}
	}

	// 204/205/304: Response constructor bu durum kodlarında body kabul etmiyor
	// (spec gereği "null body status"). .NET forgot-password gibi uçlar
	// bilinçli olarak içeriksiz 204 dönüyor.
	if ([204, 205, 304].includes(response.status)) {
		return new NextResponse(null, { status: response.status });
	}

	const contentType = response.headers.get("content-type") ?? "";
	const body = contentType.includes("application/json")
		? await response.json().catch(() => null)
		: await response.text();

	return NextResponse.json(body, { status: response.status });
}

type Params = Promise<{ path: string[] }>;

export async function GET(
	request: NextRequest,
	{ params }: { params: Params },
) {
	return forward(request, (await params).path);
}
export async function POST(
	request: NextRequest,
	{ params }: { params: Params },
) {
	return forward(request, (await params).path);
}
export async function PUT(
	request: NextRequest,
	{ params }: { params: Params },
) {
	return forward(request, (await params).path);
}
export async function PATCH(
	request: NextRequest,
	{ params }: { params: Params },
) {
	return forward(request, (await params).path);
}
export async function DELETE(
	request: NextRequest,
	{ params }: { params: Params },
) {
	return forward(request, (await params).path);
}
