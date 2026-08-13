import type { Metadata } from "next";
import { getProductsByCategory } from "@/lib/api/catalog";
import { ProductCard } from "@/components/product/product-card";
import { CategoryFilters } from "@/components/product/category-filters";

type Params = Promise<{ slug: string }>;
type SearchParams = Promise<{
	page?: string;
	minPrice?: string;
	maxPrice?: string;
	sortBy?: string;
	sortDir?: string;
	inStock?: string;
}>;

export async function generateMetadata({
	params,
}: {
	params: Params;
}): Promise<Metadata> {
	const { slug } = await params;
	return { title: `${slug} — Kategori` };
}

export default async function CategoryPage({
	params,
	searchParams,
}: {
	params: Params;
	searchParams: SearchParams;
}) {
	const { slug } = await params;
	const sp = await searchParams;

	const result = await getProductsByCategory(slug, {
		page: sp.page ? Number(sp.page) : undefined,
		minPrice: sp.minPrice ? Number(sp.minPrice) : undefined,
		maxPrice: sp.maxPrice ? Number(sp.maxPrice) : undefined,
		sortBy: sp.sortBy,
		sortDir: sp.sortDir,
		inStock: sp.inStock === "true" ? true : undefined,
	});

	return (
		<main className="container-x py-8">
			<div className="grid gap-6 md:grid-cols-[220px_1fr]">
				<CategoryFilters />

				<div>
					<p className="mb-4 text-sm text-muted-foreground">
						{result.totalCount} ürün bulundu
					</p>
					<div className="grid grid-cols-2 gap-4 sm:grid-cols-3 lg:grid-cols-4">
						{result.items.map((product) => (
							<ProductCard key={product.id} product={product} />
						))}
					</div>
					{/* Sayfalama bileşeni: result.page / totalPages üzerinden — basit
					    link listesi yeterli, tasarımı sonra iyileştir. */}
				</div>
			</div>
		</main>
	);
}
