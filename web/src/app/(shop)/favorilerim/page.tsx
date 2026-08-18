"use client";

import { useFavorites } from "@/hooks/use-favorites";
import { ProductCard } from "@/components/product/product-card";
import { Skeleton } from "@/components/ui/skeleton";

export default function FavoritesPage() {
	const { data: favorites, isLoading } = useFavorites();

	return (
		<main className="container-x py-8">
			<h1 className="mb-6 text-xl font-semibold">Favorilerim</h1>

			{isLoading ? (
				<Skeleton className="h-64 w-full" />
			) : !favorites || favorites.length === 0 ? (
				<p className="text-muted-foreground">Henüz favori ürünün yok.</p>
			) : (
				<div className="grid grid-cols-2 gap-4 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-6">
					{favorites.map((product) => (
						<ProductCard key={product.id} product={product} />
					))}
				</div>
			)}
		</main>
	);
}
