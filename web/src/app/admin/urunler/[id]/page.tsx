import { notFound } from "next/navigation";
import Link from "next/link";
import { ExternalLinkIcon } from "lucide-react";
import { serverApiFetch } from "@/lib/api/server";
import { PageHeader } from "@/components/admin/page-header";
import { ProductForm } from "@/components/admin/product-form";
import { ProductImages } from "@/components/admin/product-images";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
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
		<div className="space-y-6">
			<PageHeader
				title={product.name}
				breadcrumb={
					<p className="text-xs text-muted-foreground">
						<Link href="/admin/urunler" className="hover:text-foreground">
							Ürünler
						</Link>{" "}
						/ {product.name}
					</p>
				}
				actions={
					<>
						{product.deletedAt ? (
							<Badge variant="destructive">Silindi</Badge>
						) : product.isActive ? (
							<Badge variant="success">Aktif</Badge>
						) : (
							<Badge variant="secondary">Pasif</Badge>
						)}
						<Button
							variant="outline"
							size="sm"
							render={
								<Link href={`/urun/${product.slug}`} target="_blank" />
							}
							nativeButton={false}
						>
							<ExternalLinkIcon />
							Sitede Gör
						</Button>
					</>
				}
			/>
			<div className="grid gap-6 xl:grid-cols-[1fr_360px]">
				<ProductForm product={product} />
				<ProductImages productId={product.id as number} />
			</div>
		</div>
	);
}
