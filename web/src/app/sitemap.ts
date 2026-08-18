import type { MetadataRoute } from "next";
import { serverApiFetch } from "@/lib/api/server";
import type { components } from "@/types/api";

type CategoryDto = components["schemas"]["CategoryDto"];
type ProductListDto = components["schemas"]["ProductListDto"];
type PagedResultOfProductListDto =
	components["schemas"]["PagedResultOfProductListDto"];

const BASE_URL = process.env.NEXT_PUBLIC_SITE_URL ?? "http://localhost:3000";

export default async function sitemap(): Promise<MetadataRoute.Sitemap> {
	const categories =
		(await serverApiFetch<CategoryDto[]>("categories")) ?? [];

	const products: ProductListDto[] = [];
	let page = 1;
	while (true) {
		const result = await serverApiFetch<PagedResultOfProductListDto>(
			`products?page=${page}&pageSize=100`,
		);
		if (!result || result.items.length === 0) break;
		products.push(...result.items);
		if (!result.hasNext) break;
		page++;
	}

	return [
		{
			url: BASE_URL,
			lastModified: new Date(),
			changeFrequency: "daily",
			priority: 1,
		},
		...categories.map((c) => ({
			url: `${BASE_URL}/kategori/${c.slug}`,
			lastModified: new Date(),
			changeFrequency: "weekly" as const,
			priority: 0.7,
		})),
		...products.map((p) => ({
			url: `${BASE_URL}/urun/${p.slug}`,
			lastModified: new Date(),
			changeFrequency: "weekly" as const,
			priority: 0.5,
		})),
	];
}
