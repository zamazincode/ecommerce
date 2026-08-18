import { notFound } from "next/navigation";
import { serverApiFetch } from "@/lib/api/server";
import { ProductForm } from "@/components/admin/product-form";
import { ProductImages } from "@/components/admin/product-images";
import type { components } from "@/types/api";

type AdminProductDetailDto = components["schemas"]["AdminProductDetailDto"];

export default async function EditProductPage({
	params,
}: {
	params: Promise<{ id: string }>;
}) {
	const { id } = await params;
	const product = await serverApiFetch<AdminProductDetailDto>(
		`admin/products/${id}`,
	);
	if (!product) notFound();

	return (
		<div>
			<h1 className="mb-6 text-xl font-semibold">{product.name}</h1>
			<div className="grid gap-8 md:grid-cols-2">
				<ProductForm product={product} />
				<ProductImages productId={product.id as number} />
			</div>
		</div>
	);
}
