import type { Metadata } from "next";
import Link from "next/link";
import { PackageIcon } from "lucide-react";
import { getProductsByCategory, getCategoryTree } from "@/lib/api/catalog";
import { findCategoryPath } from "@/lib/category-path";
import { ProductCard } from "@/components/product/product-card";
import { CategoryFilters } from "@/components/product/category-filters";
import { Breadcrumb } from "@/components/common/breadcrumb";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { EmptyState } from "@/components/ui/empty-state";
import { ProductListPagination } from "@/components/product/product-list-pagination";

type Params = Promise<{ slug: string }>;
type SearchParams = Promise<{
	page?: string;
	minPrice?: string;
	maxPrice?: string;
	sortBy?: string;
	sortDir?: string;
	showOutOfStock?: string;
}>;

export async function generateMetadata({
	params,
}: {
	params: Params;
}): Promise<Metadata> {
	const { slug } = await params;
	const tree = await getCategoryTree();
	const category = findCategoryPath(tree, slug)?.at(-1);

	if (!category) return { title: "Kategori bulunamadı" };

	return {
		title: `${category.name} — Kategori`,
		description: `${category.name} kategorisindeki kitap ve ürünleri keşfedin.`,
	};
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

	const tree = await getCategoryTree();
	const path = findCategoryPath(tree, slug);
	const category = path?.at(-1);
	const children = category?.children ?? [];

	// Varsayılan: yalnızca stoktaki ürünler (dr.com.tr ile aynı davranış).
	// Checkbox işaretliyse (showOutOfStock=true) backend'e inStock HİÇ
	// gönderilmiyor — "sadece stokta olmayanlar" diye bir filtre olmadığından
	// bu, stoklu + stoksuz ürünlerin birlikte görünmesi anlamına geliyor.
	const inStock = sp.showOutOfStock === "true" ? undefined : true;

	const result = await getProductsByCategory(slug, {
		page: sp.page ? Number(sp.page) : undefined,
		minPrice: sp.minPrice ? Number(sp.minPrice) : undefined,
		maxPrice: sp.maxPrice ? Number(sp.maxPrice) : undefined,
		sortBy: sp.sortBy,
		sortDir: sp.sortDir,
		inStock,
	});

	const page = Number(result.page);
	const totalPages = Number(result.totalPages ?? 1);

	// Sayfalama linklerinin koruması gereken filtreler — `page` hariç, o
	// `ProductListPagination` tarafından ekleniyor.
	const paginationParams: Record<string, string> = {};
	if (sp.minPrice) paginationParams.minPrice = sp.minPrice;
	if (sp.maxPrice) paginationParams.maxPrice = sp.maxPrice;
	if (sp.sortBy) paginationParams.sortBy = sp.sortBy;
	if (sp.sortDir) paginationParams.sortDir = sp.sortDir;
	if (sp.showOutOfStock) paginationParams.showOutOfStock = sp.showOutOfStock;

	return (
		<main className="container-x py-6">
			{path ? (
				<Breadcrumb
					items={path.map((c) => ({
						label: c.name,
						href: `/kategori/${c.slug}`,
					}))}
				/>
			) : null}

			<div className="mt-3 mb-6">
				<h1 className="font-heading text-2xl font-semibold">
					{category?.name ?? slug}
				</h1>
				<p className="mt-1 text-sm text-muted-foreground">
					{result.totalCount} ürün
				</p>

				{children.length > 0 ? (
					<div className="mt-4 flex flex-wrap gap-2">
						{children.map((child) => (
							<Badge
								key={child.id}
								variant="outline"
								render={<Link href={`/kategori/${child.slug}`} />}
								className="rounded-full px-3 py-1.5 hover:bg-primary-soft hover:text-primary"
							>
								{child.name}
							</Badge>
						))}
					</div>
				) : null}
			</div>

			<div className="grid gap-6 lg:grid-cols-[16rem_1fr]">
				<CategoryFilters />

				<div>
					{result.items.length === 0 ? (
						<EmptyState
							icon={PackageIcon}
							title="Bu filtrelerle ürün bulunamadı"
							description="Fiyat aralığını genişletmeyi ya da stok filtresini kaldırmayı deneyin."
							action={
								<Button
									variant="outline"
									render={<Link href={`/kategori/${slug}`} />}
									nativeButton={false}
								>
									Filtreleri temizle
								</Button>
							}
						/>
					) : (
						<>
							<div className="grid grid-cols-2 gap-4 sm:grid-cols-3 sm:gap-5 lg:grid-cols-4 xl:grid-cols-5">
								{result.items.map((product) => (
									<ProductCard key={product.id} product={product} />
								))}
							</div>

							<ProductListPagination
								className="mt-8"
								basePath={`/kategori/${slug}`}
								params={paginationParams}
								page={page}
								totalPages={totalPages}
								hasPrevious={Boolean(result.hasPrevious)}
								hasNext={Boolean(result.hasNext)}
							/>
						</>
					)}
				</div>
			</div>
		</main>
	);
}
