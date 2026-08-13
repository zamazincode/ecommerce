import Image from "next/image";
import Link from "next/link";
import { formatPrice } from "@/lib/format";
import { Card } from "@/components/ui/card";
import type { ProductListDto } from "@/types";

export function ProductCard({ product }: { product: ProductListDto }) {
	const hasDiscount =
		product.discountedPrice != null &&
		product.discountedPrice < product.price;

	return (
		<Card className="p-3">
			<Link href={`/urun/${product.slug}`} className="block">
				<div className="relative aspect-2/3 w-full overflow-hidden rounded-md bg-muted">
					{product.imageUrl ? (
						<Image
							src={product.imageUrl}
							alt={product.name}
							fill
							sizes="(max-width: 640px) 50vw, (max-width: 1024px) 25vw, 200px"
							className="object-cover"
						/>
					) : null}
				</div>

				<h3 className="mt-2 line-clamp-2 text-sm font-medium">
					{product.name}
				</h3>
				{product.authorNames ? (
					<p className="text-xs text-muted-foreground">
						{product.authorNames}
					</p>
				) : null}

				<div className="mt-1 flex items-baseline gap-2">
					<span className="text-sm font-semibold">
						{formatPrice(product.effectivePrice as number)}
					</span>
					{hasDiscount ? (
						<span className="text-xs text-muted-foreground line-through">
							{formatPrice(product.price as number)}
						</span>
					) : null}
				</div>

				{!product.inStock ? (
					<p className="mt-1 text-xs text-destructive">Stokta yok</p>
				) : null}
			</Link>
		</Card>
	);
}
