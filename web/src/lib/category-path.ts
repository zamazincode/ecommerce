import type { components } from "@/types/api";

type CategoryTreeDto = components["schemas"]["CategoryTreeDto"];

/**
 * Kategori ağacında verilen slug'a giden yolu kökten yaprağa döndürür.
 * Breadcrumb ve `generateMetadata` gerçek kategori adına buradan ulaşıyor —
 * `getProductsByCategory` yalnızca ürünleri döndürüyor, kategorinin kendi
 * adını/atalarını içermiyor.
 */
export function findCategoryPath(
	tree: CategoryTreeDto[],
	slug: string,
): CategoryTreeDto[] | null {
	for (const category of tree) {
		if (category.slug === slug) return [category];

		const childPath = findCategoryPath(category.children ?? [], slug);
		if (childPath) return [category, ...childPath];
	}

	return null;
}
