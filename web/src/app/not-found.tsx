import Link from "next/link";
import { Button } from "@/components/ui/button";

export default function RootNotFound() {
	return (
		<main className="container-x flex flex-col items-center justify-center py-24 text-center">
			<h1 className="text-2xl font-semibold">404 — Sayfa bulunamadı</h1>
			<p className="mt-2 text-muted-foreground">
				Aradığın sayfa kaldırılmış ya da hiç var olmamış olabilir.
			</p>
			<Button className="mt-6">
				<Link href="/">Anasayfaya Dön</Link>
			</Button>
		</main>
	);
}
