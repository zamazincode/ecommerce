import { serverApiFetch } from "@/lib/api/server";
import { CategoryChart } from "@/components/admin/category-chart-lazy";
import type { components } from "@/types/api";

type CategoryDto = components["schemas"]["CategoryDto"];

export async function CategoryChartSection() {
	const categories = await serverApiFetch<CategoryDto[]>("categories");
	return <CategoryChart categories={categories ?? []} />;
}
