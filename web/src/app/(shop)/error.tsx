"use client";

import { useEffect } from "react";
import { AlertCircleIcon } from "lucide-react";
import { Button } from "@/components/ui/button";
import { EmptyState } from "@/components/ui/empty-state";

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
		<main className="container-x flex min-h-[50vh] items-center justify-center py-16">
			<EmptyState
				icon={AlertCircleIcon}
				tone="danger"
				title="Bir şeyler ters gitti"
				description="Sayfa yüklenirken bir hata oluştu. Tekrar dene."
				action={
					<Button onClick={() => reset()}>Tekrar Dene</Button>
				}
			/>
		</main>
	);
}
