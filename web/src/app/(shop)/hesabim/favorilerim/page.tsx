"use client";

import Link from "next/link";
import { HeartIcon } from "lucide-react";
import { useFavorites } from "@/hooks/use-favorites";
import { ProductCard } from "@/components/product/product-card";
import { ProductCardSkeleton } from "@/components/product/product-card-skeleton";
import { PageHeader } from "@/components/admin/page-header";
import { Button } from "@/components/ui/button";
import { EmptyState } from "@/components/ui/empty-state";

export default function FavoritesPage() {
	const { data: favorites, isLoading } = useFavorites();

	return (
		<div>
			<PageHeader title="Favorilerim" className="mb-6" />

			{isLoading ? (
				<div className="grid grid-cols-2 gap-4 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-5">
					{Array.from({ length: 10 }).map((_, i) => (
						<ProductCardSkeleton key={i} />
					))}
				</div>
			) : !favorites || favorites.length === 0 ? (
				<EmptyState
					icon={HeartIcon}
					title="Henüz favori ürünün yok"
					description="Beğendiğin ürünleri kalp ikonuyla favorilerine ekleyebilirsin."
					action={
						<Button render={<Link href="/" />} nativeButton={false}>
							Ürünleri Keşfet
						</Button>
					}
				/>
			) : (
				<div className="grid grid-cols-2 gap-4 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-5">
					{favorites.map((product) => (
						<ProductCard key={product.id} product={product} />
					))}
				</div>
			)}
		</div>
	);
}
