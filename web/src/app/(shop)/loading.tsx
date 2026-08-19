import { ProductCardSkeleton } from "@/components/product/product-card-skeleton";

export default function ShopLoading() {
	return (
		<main className="container-x py-8">
			<div className="grid grid-cols-2 gap-4 sm:grid-cols-3 lg:grid-cols-4">
				{Array.from({ length: 10 }).map((_, i) => (
					<ProductCardSkeleton key={i} />
				))}
			</div>
		</main>
	);
}
