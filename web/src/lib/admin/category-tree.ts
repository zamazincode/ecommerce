import type { components } from "@/types/api";

type AdminCategoryDto = components["schemas"]["AdminCategoryDto"];

export function categoryDepth(
	category: AdminCategoryDto,
	all: AdminCategoryDto[],
): number {
	let depth = 0;
	let current = category;
	while (current.parentId) {
		const parent = all.find((c) => c.id === current.parentId);
		if (!parent) break;
		depth++;
		current = parent;
	}
	return depth;
}

/** Kendisi + tüm alt kategorileri (backend'in GetSelfAndDescendantIdsAsync'inin
 * İSTEMCİ KOPYASI — yalnızca "bu seçenekleri devre dışı bırak" UX'i için.
 * Gerçek döngü koruması HER ZAMAN sunucuda (AdminCategoryService.cs). */
export function selfAndDescendantIds(
	categoryId: number,
	all: AdminCategoryDto[],
): Set<number> {
	const result = new Set<number>([categoryId]);
	let added = true;
	while (added) {
		added = false;
		for (const c of all) {
			const parentId = c.parentId as number | null;
			const id = c.id as number;
			if (parentId !== null && result.has(parentId) && !result.has(id)) {
				result.add(id);
				added = true;
			}
		}
	}
	return result;
}
