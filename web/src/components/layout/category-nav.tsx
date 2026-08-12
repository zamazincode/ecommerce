import Link from "next/link";
import { serverApiFetch } from "@/lib/api/server";
import type { components } from "@/types/api";

type CategoryTreeDto = components["schemas"]["CategoryTreeDto"];

export async function CategoryNav() {
	const tree =
		(await serverApiFetch<CategoryTreeDto[]>("categories/tree")) ?? [];

	return (
		<nav className="flex gap-6 overflow-x-auto text-sm">
			{tree.map((category) => (
				<Link
					key={category.id}
					href={`/kategori/${category.slug}`}
					className="whitespace-nowrap hover:underline"
				>
					{category.name}
				</Link>
			))}
		</nav>
	);
}
