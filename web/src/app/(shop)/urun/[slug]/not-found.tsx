import Link from "next/link";
import { BookOpenIcon } from "lucide-react";
import { Button } from "@/components/ui/button";
import { EmptyState } from "@/components/ui/empty-state";

export default function ProductNotFound() {
	return (
		<main className="container-x flex min-h-[50vh] items-center justify-center py-16">
			<EmptyState
				icon={BookOpenIcon}
				title="Ürün bulunamadı"
				description="Aradığın ürün kaldırılmış ya da hiç var olmamış olabilir."
				action={
					<Button render={<Link href="/" />} nativeButton={false}>
						Anasayfaya Dön
					</Button>
				}
			/>
		</main>
	);
}
