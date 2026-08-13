"use client";

import { useEffect } from "react";
import { Button } from "@/components/ui/button";

export default function ShopError({
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
		<main className="container-x py-16 text-center">
			<h1 className="text-xl font-semibold">Bir şeyler ters gitti</h1>
			<p className="mt-2 text-muted-foreground">
				Sayfa yüklenirken bir hata oluştu. Tekrar dene.
			</p>
			<Button className="mt-4" onClick={() => reset()}>
				Tekrar Dene
			</Button>
		</main>
	);
}
