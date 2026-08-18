"use client";

import { useEffect } from "react";
import { AdminSidebar } from "@/components/admin/sidebar";
import { Button } from "@/components/ui/button";

export default function AdminError({
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
		<div className="flex min-h-screen">
			<AdminSidebar />
			<main className="container-x flex flex-1 flex-col items-center justify-center py-24 text-center">
				<h1 className="text-xl font-semibold">Bir şeyler ters gitti</h1>
				<p className="mt-2 text-muted-foreground">
					Sayfa yüklenirken bir hata oluştu. Tekrar dene ya da başka bir
					bölüme geç.
				</p>
				<Button className="mt-4" onClick={() => reset()}>
					Tekrar Dene
				</Button>
			</main>
		</div>
	);
}
