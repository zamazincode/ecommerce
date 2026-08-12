import Link from "next/link";
import { serverApiFetch } from "@/lib/api/server";
import type { components } from "@/types/api";

type CategoryTreeDto = components["schemas"]["CategoryTreeDto"];

export async function Footer() {
	const tree =
		(await serverApiFetch<CategoryTreeDto[]>("categories/tree")) ?? [];

	return (
		<footer className="mt-16 border-t bg-muted/30 py-10">
			<div className="container-x grid gap-8 text-sm sm:grid-cols-3">
				<div>
					<h3 className="mb-3 font-semibold">Kategoriler</h3>
					<ul className="space-y-2 text-muted-foreground">
						{tree.slice(0, 6).map((category) => (
							<li key={category.id}>
								<Link
									href={`/kategori/${category.slug}`}
									className="hover:underline"
								>
									{category.name}
								</Link>
							</li>
						))}
					</ul>
				</div>
				<div>
					<h3 className="mb-3 font-semibold">Hesabım</h3>
					<ul className="space-y-2 text-muted-foreground">
						<li>
							<Link
								href="/hesabim/siparislerim"
								className="hover:underline"
							>
								Siparişlerim
							</Link>
						</li>
						<li>
							<Link
								href="/hesabim/adreslerim"
								className="hover:underline"
							>
								Adreslerim
							</Link>
						</li>
						<li>
							<Link href="/giris" className="hover:underline">
								Giriş Yap
							</Link>
						</li>
					</ul>
				</div>
				<div>
					<h3 className="mb-3 font-semibold">İletişim</h3>
					<p className="text-muted-foreground">Sosyal medya ...</p>
				</div>
			</div>
			<p className="container-x mt-8 text-xs text-muted-foreground">
				© {new Date().getFullYear()} — Tüm hakları saklıdır.
			</p>
		</footer>
	);
}
