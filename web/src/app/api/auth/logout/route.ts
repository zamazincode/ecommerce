import { NextResponse } from "next/server";
import { clearSessionCookies, getRefreshToken } from "@/lib/api/session";

export async function POST() {
	const refreshToken = await getRefreshToken();

	if (refreshToken) {
		await fetch(`${process.env.API_INTERNAL_URL}/api/auth/logout`, {
			method: "POST",
			headers: { "Content-Type": "application/json" },
			body: JSON.stringify({ refreshToken }),
			cache: "no-store",
		}).catch(() => {});
	}

	await clearSessionCookies();
	return NextResponse.json({ success: true });
}
