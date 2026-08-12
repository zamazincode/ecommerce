import { NextResponse, type NextRequest } from "next/server";

const PROTECTED_PREFIXES = ["/hesabim", "/admin"];

export function proxy(request: NextRequest) {
	const { pathname } = request.nextUrl;

	console.log("test1");

	const isProtected = PROTECTED_PREFIXES.some((prefix) =>
		pathname.startsWith(prefix),
	);
	if (!isProtected) return NextResponse.next();

	// cookie olup da yanlış token olsa bile .Net tarafında kontrol ediliyo burada kontrol etmek performans kaybına yol açar
	const hasSession = request.cookies.get("commerce_rt");
	if (!hasSession) {
		const loginUrl = new URL("/giris", request.url);
		loginUrl.searchParams.set("returnUrl", pathname);
		return NextResponse.redirect(loginUrl);
	}

	console.log("test2");

	return NextResponse.next();
}

export const config = {
	matcher: ["/hesabim/:path*", "/admin/:path*"],
};
