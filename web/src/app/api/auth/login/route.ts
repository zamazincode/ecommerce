import { setSessionCookies } from "@/lib/api/session";
import { validate } from "@/lib/validate";
import { loginSchema } from "@/lib/validations/auth";
import { AuthResponse } from "@/types";
import { NextResponse } from "next/server";

export const POST = validate(loginSchema, async (_req, body) => {
	const apiResponse = await fetch(
		`${process.env.API_INTERNAL_URL}/api/auth/login`,
		{
			method: "POST",
			headers: { "Content-Type": "application/json" },
			body: JSON.stringify(body),
			cache: "no-store",
		},
	);

	if (!apiResponse.ok) {
		const problem = await apiResponse.json().catch(() => null);

		return NextResponse.json(problem ?? { message: "Giriş başarısız." }, {
			status: apiResponse.status,
		});
	}

	const auth = (await apiResponse.json()) as AuthResponse;

	await setSessionCookies({
		accessToken: auth.accessToken,
		refreshToken: auth.refreshToken,
	});

	return NextResponse.json({ user: auth.user });
});
