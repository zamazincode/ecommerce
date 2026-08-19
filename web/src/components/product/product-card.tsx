import Image from "next/image";
import Link from "next/link";
import { Card } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Price } from "@/components/ui/price";
import { FavoriteButton } from "@/components/product/favorite-button";
import { QuickAddButton } from "@/components/product/quick-add-button";
import type { ProductListDto } from "@/types";

export function ProductCard({ product }: { product: ProductListDto }) {
	const price = product.price as number;
	const discountedPrice = product.discountedPrice as number | null;
	const hasDiscount = discountedPrice != null && discountedPrice < price;
	const percentage = hasDiscount
		? Math.round((1 - discountedPrice / price) * 100)
		: 0;

	return (
		<Card
			interactive
			size="sm"
			className="group relative flex h-full flex-col gap-0 overflow-hidden p-0"
		>
			<FavoriteButton productId={product.id as number} />

			<Link
				href={`/urun/${product.slug}`}
				className="flex flex-1 flex-col"
			>
				<div className="relative aspect-3/4 shrink-0 bg-muted/40 p-4">
					{product.imageUrl ? (
						<Image
							src={product.imageUrl}
							alt={product.name}
							fill
							sizes="(max-width: 640px) 45vw, (max-width: 1024px) 30vw, 240px"
							className="object-contain transition-transform duration-300 group-hover:scale-105"
						/>
					) : null}

					<div className="absolute top-2 left-2 flex flex-col items-start gap-1.5">
						{hasDiscount && percentage > 0 ? (
							<Badge variant="accent">%{percentage}</Badge>
						) : null}
						{!product.inStock ? (
							<Badge variant="secondary">Stokta yok</Badge>
						) : null}
					</div>
				</div>

				<div className="flex flex-1 flex-col gap-1 p-3">
					{/* Yazar satırı ürüne göre olmayabilir (kitap dışı ürünler) —
					    yine de boş bir satır yüksekliği kadar yer ayrılır, aksi
					    hâlde aynı satırdaki kartlar farklı boyda görünür. */}
					<p className="line-clamp-1 text-[11px] tracking-wide text-muted-foreground uppercase">
						{product.authorNames || " "}
					</p>
					<h3 className="line-clamp-2 min-h-10 text-sm leading-snug font-medium group-hover:text-primary">
						{product.name}
					</h3>
					<Price
						price={price}
						discountedPrice={discountedPrice}
						size="md"
						className="mt-auto pt-1"
					/>
				</div>
			</Link>

			<QuickAddButton
				productId={product.id as number}
				productName={product.name}
				inStock={!!product.inStock}
			/>
		</Card>
	);
}
