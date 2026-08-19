import Link from "next/link";
import { ScrollCarousel } from "@/components/ui/scroll-carousel";
import { getCategoryIcon } from "@/lib/category-icons";
import type { components } from "@/types/api";

type CategoryTreeDto = components["schemas"]["CategoryTreeDto"];

/**
 * "Senin İçin Seçtiklerimiz" — kök kategorilerin 2. seviye çocuklarından 8
 * tanesi. Kategori görseli API'de yok (ölçüldü) — ikon + pastel zemin.
 */
export function CollectionTiles({
	categories,
}: {
	categories: CategoryTreeDto[];
}) {
	const tiles = categories.flatMap((root) => root.children ?? []).slice(0, 8);
	if (tiles.length === 0) return null;

	return (
		<ScrollCarousel>
			{tiles.map((category) => {
				const Icon = getCategoryIcon(category.slug);
				return (
					<Link
						key={category.id}
						href={`/kategori/${category.slug}`}
						className="group w-40"
					>
						<div className="grid aspect-square place-items-center rounded-2xl bg-primary-soft text-primary transition-colors group-hover:bg-primary group-hover:text-primary-foreground">
							<Icon className="size-12" />
						</div>
						<p className="mt-2 line-clamp-2 text-center text-sm font-medium">
							{category.name}
						</p>
					</Link>
				);
			})}
		</ScrollCarousel>
	);
}
