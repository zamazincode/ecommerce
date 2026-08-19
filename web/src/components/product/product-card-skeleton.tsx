import { Card } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";

/**
 * `ProductCard`'ın yükleniyor hâli — anasayfa/kategori/arama/favoriler
 * grid'leriyle aynı boyut sözleşmesini paylaşır (aspect-3/4 görsel + iki
 * satır + fiyat çizgisi). Tüm ürün grid'i `loading.tsx`'leri bunu kullanır.
 */
export function ProductCardSkeleton() {
	return (
		<Card size="sm" className="flex flex-col gap-0 overflow-hidden p-0">
			<Skeleton className="aspect-3/4 w-full rounded-none" />
			<div className="space-y-2 p-3">
				<Skeleton className="h-3 w-2/3" />
				<Skeleton className="h-4 w-full" />
				<Skeleton className="mt-1 h-4 w-1/3" />
			</div>
		</Card>
	);
}
