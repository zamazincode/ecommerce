"use client";

import "./globals.css";
import { useEffect } from "react";
import { AlertCircleIcon } from "lucide-react";
import { EmptyState } from "@/components/ui/empty-state";

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
			<body className="grid min-h-screen place-items-center px-4">
				<EmptyState
					icon={AlertCircleIcon}
					tone="danger"
					title="Uygulama çöktü"
					description="Beklenmeyen bir hata oluştu. Sayfayı yenilemeyi dene."
					action={
						<button
							type="button"
							className="rounded-md border px-4 py-2 text-sm hover:bg-muted"
							onClick={() => reset()}
						>
							Tekrar Dene
						</button>
					}
				/>
			</body>
		</html>
	);
}
