"use client";

import { useEffect, useState } from "react";
import { useSearchParams } from "next/navigation";
import Link from "next/link";
import { CheckIcon, LoaderIcon, XIcon } from "lucide-react";
import { apiFetch } from "@/lib/api/client";
import { Button } from "@/components/ui/button";
import { cn } from "@/lib/utils";
import Logo from "@/components/common/logo";

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
	const isLoading = status === "loading" && !isParamsMissing;

	return (
		<div className="w-full max-w-md rounded-2xl border bg-card p-8 text-center shadow-card">
			<Link href="/" className="mb-6 flex justify-center">
				<Logo />
			</Link>

			{isLoading ? (
				<div className="flex flex-col items-center gap-3">
					<LoaderIcon className="size-8 animate-spin text-primary" />
					<p className="text-sm text-muted-foreground">
						Doğrulanıyor…
					</p>
				</div>
			) : null}

			{!isLoading && status === "success" ? (
				<div className="flex flex-col items-center gap-3">
					<div
						className={cn(
							"grid size-14 place-items-center rounded-full bg-success-soft text-success",
						)}
					>
						<CheckIcon className="size-7" />
					</div>
					<h1 className="text-lg font-semibold">
						E-postan doğrulandı
					</h1>
					<Button render={<Link href="/" />} nativeButton={false}>
						Ana Sayfaya Dön
					</Button>
				</div>
			) : null}

			{!isLoading && isError ? (
				<div className="flex flex-col items-center gap-3">
					<div className="grid size-14 place-items-center rounded-full bg-destructive/10 text-destructive">
						<XIcon className="size-7" />
					</div>
					<h1 className="text-lg font-semibold">
						Doğrulama başarısız
					</h1>
					<p className="text-sm text-muted-foreground">
						Bağlantının süresi dolmuş olabilir ya da zaten
						kullanılmış.
					</p>
					<Button
						variant="outline"
						render={<Link href="/giris" />}
						nativeButton={false}
					>
						Girişe Dön
					</Button>
				</div>
			) : null}
		</div>
	);
}
