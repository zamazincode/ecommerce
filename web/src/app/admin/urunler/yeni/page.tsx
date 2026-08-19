import { PageHeader } from "@/components/admin/page-header";
import { ProductForm } from "@/components/admin/product-form";

export default function NewProductPage() {
	return (
		<div className="space-y-6">
			<PageHeader
				title="Yeni Ürün"
				description="Yeni bir katalog kaydı oluştur"
			/>
			<ProductForm />
		</div>
	);
}
