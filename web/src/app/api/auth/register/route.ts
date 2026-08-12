import { NextResponse } from "next/server";
import { registerSchema } from "@/lib/validations/auth";
import { setSessionCookies } from "@/lib/api/session";
import type { AuthResponse } from "@/types";
import { validate } from "@/lib/validate";

export const POST = validate(registerSchema, async (_req, body) => {
	const apiResponse = await fetch(
		`${process.env.API_INTERNAL_URL}/api/auth/register`,
		{
			method: "POST",
			headers: { "Content-Type": "application/json" },
			body: JSON.stringify(body),
			cache: "no-store",
		},
	);

	if (!apiResponse.ok) {
		const problem = await apiResponse.json().catch(() => null);
		return NextResponse.json(problem ?? { message: "Kayıt başarısız." }, {
			status: apiResponse.status,
		});
	}

	const auth = (await apiResponse.json()) as AuthResponse;
	await setSessionCookies({
		accessToken: auth.accessToken,
		refreshToken: auth.refreshToken,
	});

	return NextResponse.json({ user: auth.user }, { status: 201 });
});
