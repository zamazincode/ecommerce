import Link from "next/link";
import { ChevronRightIcon, HouseIcon } from "lucide-react";
import { toSafeJsonLd } from "@/lib/utils";

export interface BreadcrumbItem {
	label: string;
	/** Son öğede verilmez — o zaten geçerli sayfa, tıklanamaz metin olarak basılır. */
	href?: string;
}

/**
 * Kategori/ürün gibi derin sayfalarda üst gezinme + Google'ın zengin
 * sonuçlarında kullandığı `BreadcrumbList` JSON-LD'sini birlikte üretir.
 * "Ana Sayfa" öğesini kendisi ekler, çağıran yalnızca kategori zincirini verir.
 */
export function Breadcrumb({ items }: { items: BreadcrumbItem[] }) {
	const allItems: BreadcrumbItem[] = [{ label: "Ana Sayfa", href: "/" }, ...items];

	const jsonLd = {
		"@context": "https://schema.org",
		"@type": "BreadcrumbList",
		itemListElement: allItems.map((item, index) => ({
			"@type": "ListItem",
			position: index + 1,
			name: item.label,
			...(item.href ? { item: item.href } : {}),
		})),
	};

	return (
		<nav aria-label="Breadcrumb" className="text-sm text-muted-foreground">
			<script
				type="application/ld+json"
				dangerouslySetInnerHTML={{ __html: toSafeJsonLd(jsonLd) }}
			/>
			<ol className="flex flex-wrap items-center gap-1.5">
				{allItems.map((item, index) => {
					const isLast = index === allItems.length - 1;
					return (
						<li key={index} className="flex items-center gap-1.5">
							{index === 0 ? <HouseIcon className="size-3.5" /> : null}
							{item.href && !isLast ? (
								<Link
									href={item.href}
									className="hover:text-primary hover:underline"
								>
									{item.label}
								</Link>
							) : (
								<span
									className={isLast ? "font-medium text-foreground" : undefined}
								>
									{item.label}
								</span>
							)}
							{!isLast ? (
								<ChevronRightIcon className="size-3.5 text-muted-foreground/60" />
							) : null}
						</li>
					);
				})}
			</ol>
		</nav>
	);
}
