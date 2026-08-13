import "server-only";

import type { ProductDetailDto, PagedResultOfProductListDto } from "@/types";
import type { components } from "@/types/api";
import { ApiError } from "@/lib/api/client";

type HomeDto = components["schemas"]["HomeDto"];
type CategoryTreeDto = components["schemas"]["CategoryTreeDto"];
type SearchResultDto = components["schemas"]["SearchResultDto"];

const API_BASE = process.env.API_INTERNAL_URL;

export async function getHome(): Promise<HomeDto> {
	const response = await fetch(`${API_BASE}/api/home`, {
		next: { revalidate: 60, tags: ["home"] },
	});
	if (!response.ok) throw new Error("Ana sayfa verisi alınamadı.");
	return response.json();
}

export async function getProductBySlug(
	slug: string,
): Promise<ProductDetailDto | null> {
	const response = await fetch(`${API_BASE}/api/products/${slug}`, {
		next: { revalidate: 60, tags: ["products", `product:${slug}`] },
	});
	if (response.status === 404) return null;
	if (!response.ok) throw new Error("Ürün verisi alınamadı.");
	return response.json();
}

export interface ProductFilters {
	page?: number;
	pageSize?: number;
	categoryId?: number;
	authorId?: number;
	publisherId?: number;
	minPrice?: number;
	maxPrice?: number;
	inStock?: boolean;
	sortBy?: string;
	sortDir?: string;
}

function buildQuery(filters: object): string {
	const params = new URLSearchParams();
	for (const [key, value] of Object.entries(
		filters as Record<string, unknown>,
	)) {
		if (value === undefined || value === null || value === "") continue;
		params.set(key, String(value));
	}
	const query = params.toString();
	return query ? `?${query}` : "";
}

export async function getProductsByCategory(
	slug: string,
	filters: ProductFilters,
): Promise<PagedResultOfProductListDto> {
	const response = await fetch(
		`${API_BASE}/api/categories/${slug}/products${buildQuery(filters)}`,
		// SSR — kategori sayfası filtre kombinasyonuna göre HER ZAMAN taze.
		{ cache: "no-store" },
	);
	if (!response.ok) throw new Error("Kategori ürünleri alınamadı.");
	return response.json();
}

export async function search(
	query: string,
	filters: Omit<
		ProductFilters,
		"authorId" | "publisherId" | "sortBy" | "sortDir"
	>,
): Promise<SearchResultDto> {
	const response = await fetch(
		`${API_BASE}/api/search${buildQuery({ q: query, ...filters })}`,
		{ cache: "no-store" },
	);
	if (!response.ok) {
		throw new ApiError(response.status, await response.json().catch(() => null));
	}
	return response.json();
}

export async function getCategoryTree(): Promise<CategoryTreeDto[]> {
	const response = await fetch(`${API_BASE}/api/categories/tree`, {
		// Kategori ağacı neredeyse hiç değişmiyor — uzun revalidate.
		next: { revalidate: 3600, tags: ["categories"] },
	});
	if (!response.ok) throw new Error("Kategori ağacı alınamadı.");
	return response.json();
}
