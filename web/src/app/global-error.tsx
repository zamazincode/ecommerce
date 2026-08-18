"use client";

import "./globals.css";
import { useEffect } from "react";

export default function GlobalError({
	error,
	reset,
}: {
	error: Error & { digest?: string };
	reset: () => void;
}) {
	useEffect(() => {
		console.error(error);
	}, [error]);

	return (
		<html lang="tr">
			<body className="flex min-h-screen items-center justify-center">
				<div className="text-center">
					<h1 className="text-xl font-semibold">Uygulama çöktü</h1>
					<p className="mt-2 text-muted-foreground">
						Beklenmeyen bir hata oluştu. Sayfayı yenilemeyi dene.
					</p>
					<button
						className="mt-4 rounded-md border px-4 py-2 text-sm"
						onClick={() => reset()}
					>
						Tekrar Dene
					</button>
				</div>
			</body>
		</html>
	);
}
