"use client";

import { useEffect } from "react";
import Link from "next/link";
import { AlertCircleIcon } from "lucide-react";
import { AdminSidebar } from "@/components/admin/sidebar";
import { Button } from "@/components/ui/button";
import { EmptyState } from "@/components/ui/empty-state";

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
		<div className="flex min-h-screen bg-surface">
			<AdminSidebar />
			<main className="flex flex-1 items-center justify-center px-4 py-6 sm:px-6 lg:px-8">
				<EmptyState
					icon={AlertCircleIcon}
					tone="danger"
					title="Bir şeyler ters gitti"
					description="Sayfa yüklenirken bir hata oluştu. Tekrar dene ya da başka bir bölüme geç."
					action={
						<div className="flex items-center justify-center gap-2">
							<Button variant="outline" onClick={() => reset()}>
								Tekrar Dene
							</Button>
							<Button render={<Link href="/admin" />} nativeButton={false}>
								Dashboard&apos;a Dön
							</Button>
						</div>
					}
				/>
			</main>
		</div>
	);
}
