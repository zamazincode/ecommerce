"use client";

import { Pagination } from "@/components/ui/pagination";

interface ProductListPaginationProps {
	basePath: string;
	/** Sayfa numarası hariç, korunması gereken tüm filtre parametreleri. */
	params: Record<string, string>;
	page: number;
	totalPages: number;
	hasPrevious: boolean;
	hasNext: boolean;
	className?: string;
}

/**
 * `Pagination`'ın link modu bir `buildHref` fonksiyonu bekliyor — bir Server
 * Component'ten (kategori/arama sayfaları) doğrudan fonksiyon prop'u geçmek
 * "Functions cannot be passed directly to Client Components" hatası verir
 * (ölçüldü). Bu ince client sarmalayıcı, yalnızca serileştirilebilir veri
 * (string/number/boolean) alır ve `buildHref`'i kendi içinde üretir.
 */
export function ProductListPagination({
	basePath,
	params,
	page,
	totalPages,
	hasPrevious,
	hasNext,
	className,
}: ProductListPaginationProps) {
	function buildHref(targetPage: number) {
		const usp = new URLSearchParams(params);
		usp.set("page", String(targetPage));
		return `${basePath}?${usp.toString()}`;
	}

	return (
		<Pagination
			className={className}
			page={page}
			totalPages={totalPages}
			hasPrevious={hasPrevious}
			hasNext={hasNext}
			buildHref={buildHref}
		/>
	);
}
