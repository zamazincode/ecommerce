import { describe, expect, test } from "vitest";
import { selfAndDescendantIds } from "@/lib/admin/category-tree";
import type { components } from "@/types/api";

type AdminCategoryDto = components["schemas"]["AdminCategoryDto"];

const categories: AdminCategoryDto[] = [
	{
		id: 1,
		name: "Kitap",
		slug: "kitap",
		parentId: null,
		displayOrder: 0,
		isActive: true,
		productCount: 0,
	},
	{
		id: 2,
		name: "Roman",
		slug: "roman",
		parentId: 1,
		displayOrder: 0,
		isActive: true,
		productCount: 0,
	},
	{
		id: 3,
		name: "Dünya Roman",
		slug: "dunya-roman",
		parentId: 2,
		displayOrder: 0,
		isActive: true,
		productCount: 0,
	},
	{
		id: 4,
		name: "Elektronik",
		slug: "elektronik",
		parentId: null,
		displayOrder: 0,
		isActive: true,
		productCount: 0,
	},
];

describe("selfAndDescendantIds", () => {
	test("kök kategorinin torunları (çok seviyeli) doğru toplanıyor", () => {
		const ids = selfAndDescendantIds(1, categories);
		expect(ids).toEqual(new Set([1, 2, 3]));
	});

	test("ilgisiz bir dal sonuca karışmıyor", () => {
		const ids = selfAndDescendantIds(1, categories);
		expect(ids.has(4)).toBe(false);
	});

	test("yaprak kategorinin sadece kendisi var", () => {
		expect(selfAndDescendantIds(3, categories)).toEqual(new Set([3]));
	});
});
