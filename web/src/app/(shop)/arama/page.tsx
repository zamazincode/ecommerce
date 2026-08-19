import Link from "next/link";
import { SearchIcon, SearchXIcon, SparklesIcon } from "lucide-react";
import { search, getCategoryTree } from "@/lib/api/catalog";
import { ApiError } from "@/lib/api/client";
import { ProductCard } from "@/components/product/product-card";
import { CategoryFilters } from "@/components/product/category-filters";
import { CategoryStrip } from "@/components/layout/category-strip";
import { EmptyState } from "@/components/ui/empty-state";
import { ProductListPagination } from "@/components/product/product-list-pagination";

type SearchParams = Promise<{
	q?: string;
	page?: string;
	minPrice?: string;
	maxPrice?: string;
	showOutOfStock?: string;
}>;

const TOO_SHORT_MESSAGE = "Aramak için en az 2 karakter yazın.";

export default async function SearchPage({
	searchParams,
}: {
	searchParams: SearchParams;
}) {
	const sp = await searchParams;
	const { q } = sp;

	if (!q || q.trim().length < 2) {
		return (
			<main className="container-x py-8">
				<EmptyState icon={SearchIcon} title={TOO_SHORT_MESSAGE} />
			</main>
		);
	}

	// Kategori sayfasıyla aynı davranış: varsayılan yalnızca stoktaki ürünler,
	// checkbox işaretliyse (showOutOfStock=true) backend'e inStock hiç
	// gönderilmiyor — stoklu + stoksuz ürünler birlikte görünür.
	const inStock = sp.showOutOfStock === "true" ? undefined : true;

	let result;
	try {
		result = await search(q, {
			page: sp.page ? Number(sp.page) : undefined,
			minPrice: sp.minPrice ? Number(sp.minPrice) : undefined,
			maxPrice: sp.maxPrice ? Number(sp.maxPrice) : undefined,
			inStock,
		});
	} catch (error) {
		// Girdi 2 karakter olsa bile normalizasyon sonrası (aksan/özel
		// karakter temizliği) kısalabiliyor — bu durumda backend 400 döner.
		// Bu, sistem hatası değil kullanıcı girdisi sorunu; error.tsx'e düşürme.
		if (error instanceof ApiError && error.status === 400) {
			return (
				<main className="container-x py-8">
					<EmptyState icon={SearchIcon} title={TOO_SHORT_MESSAGE} />
				</main>
			);
		}
		throw error;
	}

	const totalCount = Number(result.results.totalCount);
	const page = Number(result.results.page);
	const totalPages = Number(result.results.totalPages ?? 1);

	// Sayfalama linklerinin koruması gereken parametreler — `page` hariç, o
	// `ProductListPagination` tarafından ekleniyor.
	const paginationParams: Record<string, string> = { q: q as string };
	if (sp.minPrice) paginationParams.minPrice = sp.minPrice;
	if (sp.maxPrice) paginationParams.maxPrice = sp.maxPrice;
	if (sp.showOutOfStock) paginationParams.showOutOfStock = sp.showOutOfStock;

	return (
		<main className="container-x py-8">
			<h1 className="font-heading text-xl font-semibold">
				&quot;{result.term}&quot; için {totalCount} sonuç
			</h1>

			{totalCount === 0 && result.didYouMean ? (
				<div className="mt-4 flex items-center gap-2 rounded-xl bg-warning-soft p-4 text-sm text-warning">
					<SparklesIcon className="size-4 shrink-0" />
					<p>
						Bunu mu demek istediniz:{" "}
						<Link
							href={`/arama?q=${encodeURIComponent(result.didYouMean)}`}
							className="font-medium underline underline-offset-2 hover:no-underline"
						>
							{result.didYouMean}
						</Link>
						?
					</p>
				</div>
			) : null}

			{totalCount === 0 ? (
				<>
					<EmptyState
						icon={SearchXIcon}
						title={`"${result.term}" için sonuç bulunamadı`}
						description="Farklı bir anahtar kelime deneyin ya da aşağıdaki popüler kategorilere göz atın."
					/>

					<div className="mt-8">
						<p className="mb-4 text-sm font-medium">Popüler kategoriler</p>
						<CategoryStrip categories={await getCategoryTree()} />
					</div>
				</>
			) : (
				<div className="mt-6 grid gap-6 lg:grid-cols-[16rem_1fr]">
					{/* DOM'da grid'den SONRA gelir (checkout.spec.ts:6 <main> içindeki
					    ilk linkin bir ürün linki olmasını bekliyor), `order-first` ile
					    yalnızca GÖRSEL olarak sola alınır. */}
					<div className="lg:order-2">
						<div className="grid grid-cols-2 gap-4 sm:grid-cols-3 sm:gap-5 lg:grid-cols-4">
							{result.results.items.map((product) => (
								<ProductCard key={product.id} product={product} />
							))}
						</div>

						<ProductListPagination
							className="mt-8"
							basePath="/arama"
							params={paginationParams}
							page={page}
							totalPages={totalPages}
							hasPrevious={Boolean(result.results.hasPrevious)}
							hasNext={Boolean(result.results.hasNext)}
						/>
					</div>

					<CategoryFilters showSort={false} className="lg:order-1" />
				</div>
			)}
		</main>
	);
}
