import Link from "next/link";
import { CompassIcon } from "lucide-react";
import { Button } from "@/components/ui/button";
import { EmptyState } from "@/components/ui/empty-state";

export default function RootNotFound() {
	return (
		<main className="container-x flex min-h-[60vh] items-center justify-center py-16">
			<EmptyState
				icon={CompassIcon}
				title="404 — Sayfa bulunamadı"
				description="Aradığın sayfa kaldırılmış ya da hiç var olmamış olabilir."
				action={
					<Button
						render={<Link href="/" />}
						nativeButton={false}
					>
						Anasayfaya Dön
					</Button>
				}
			/>
		</main>
	);
}
