"use client";

import { useEffect, useState } from "react";
import { useSearchParams } from "next/navigation";
import Link from "next/link";
import { apiFetch } from "@/lib/api/client";

// "error" durumunu baştan yakalayacağımız için state sadece loading ve success yönetebilir
type Status = "loading" | "success" | "error";

export default function VerifyEmailPage() {
	const searchParams = useSearchParams();
	const [status, setStatus] = useState<Status>("loading");

	const email = searchParams.get("email");
	const token = searchParams.get("token");

	const isParamsMissing = !email || !token;

	useEffect(() => {
		if (isParamsMissing) return;

		apiFetch("auth/verify-email", {
			method: "POST",
			body: JSON.stringify({ email, token }),
		})
			.then(() => setStatus("success"))
			.catch(() => setStatus("error"));
	}, [email, token, isParamsMissing]); // Bağımlılıkları ekledik

	const isError = isParamsMissing || status === "error";

	return (
		<main className="container-x py-16 text-center">
			{status === "loading" && !isParamsMissing ? (
				<p>Doğrulanıyor…</p>
			) : null}

			{status === "success" ? (
				<>
					<h1 className="text-xl font-semibold">
						E-postan doğrulandı
					</h1>
					<Link href="/" className="mt-4 inline-block underline">
						Ana sayfaya dön
					</Link>
				</>
			) : null}

			{isError ? (
				<>
					<h1 className="text-xl font-semibold">
						Doğrulama başarısız
					</h1>
					<p className="mt-2 text-muted-foreground">
						Bağlantının süresi dolmuş olabilir ya da zaten
						kullanılmış.
					</p>
				</>
			) : null}
		</main>
	);
}
