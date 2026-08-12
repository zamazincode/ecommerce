import { getAccessToken } from "@/lib/api/session";
import { NextResponse } from "next/server";

export async function GET() {
	const accessToken = await getAccessToken();

	if (!accessToken) return NextResponse.json({ user: null }, { status: 200 });

	const apiResponse = await fetch(
		`${process.env.API_INTERNAL_URL}/api/auth/me`,
		{
			headers: { Authorization: `Bearer ${accessToken}` },
			cache: "no-store",
		},
	);

	if (!apiResponse.ok) {
		return NextResponse.json({ user: null }, { status: 200 });
	}

	const user = await apiResponse.json();
	return NextResponse.json({ user });
}
