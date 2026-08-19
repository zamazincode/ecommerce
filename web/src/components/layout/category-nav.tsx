import { serverApiFetch } from "@/lib/api/server";
import { MegaMenu } from "./mega-menu";
import type { components } from "@/types/api";

type CategoryTreeDto = components["schemas"]["CategoryTreeDto"];

export async function CategoryNav() {
	const tree =
		(await serverApiFetch<CategoryTreeDto[]>("categories/tree")) ?? [];

	return <MegaMenu tree={tree} />;
}
