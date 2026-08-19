import Link from "next/link";
import { FlameIcon } from "lucide-react";
import { ScrollCarousel } from "@/components/ui/scroll-carousel";
import { getCategoryIcon } from "@/lib/category-icons";
import type { components } from "@/types/api";

type CategoryTreeDto = components["schemas"]["CategoryTreeDto"];

const tileClass = "group flex w-24 flex-col items-center gap-2";
const iconWrapClass =
	"grid size-16 place-items-center rounded-2xl bg-primary-soft text-primary transition-all group-hover:-translate-y-1 group-hover:bg-primary group-hover:text-primary-foreground group-hover:shadow-card-hover";

/**
 * dr.com.tr'nin ikon şeridi. 7 kök kategori + sabit "Çok Satanlar" = 8 öğe,
 * mobilde yatay kayıyor, `lg`'de 8 sütunluk sabit bir ızgaraya dönüşüyor.
 */
export function CategoryStrip({
	categories,
}: {
	categories: CategoryTreeDto[];
}) {
	return (
		<div className="container-x">
			<ScrollCarousel className="lg:grid lg:grid-cols-8 lg:overflow-visible">
				{categories.map((category) => {
					const Icon = getCategoryIcon(category.slug);
					return (
						<Link
							key={category.id}
							href={`/kategori/${category.slug}`}
							className={tileClass}
						>
							<div className={iconWrapClass}>
								<Icon className="size-7" />
							</div>
							<span className="line-clamp-2 text-center text-xs font-medium">
								{category.name}
							</span>
						</Link>
					);
				})}
				<Link href="/kategori/kitap" className={tileClass}>
					<div className={iconWrapClass}>
						<FlameIcon className="size-7" />
					</div>
					<span className="text-center text-xs font-medium">
						Çok Satanlar
					</span>
				</Link>
			</ScrollCarousel>
		</div>
	);
}
