import Link from "next/link";
import { Button } from "@/components/ui/button";

export default function ProductNotFoundAdmin() {
	return (
		<div className="py-16 text-center">
			<h1 className="text-xl font-semibold">Ürün bulunamadı</h1>
			<p className="mt-2 text-muted-foreground">
				Bu id&apos;ye ait bir ürün yok — silinmiş ya da hiç var olmamış
				olabilir.
			</p>
			<Button className="mt-4">
				<Link href="/admin/urunler">Ürünlere Dön</Link>
			</Button>
		</div>
	);
}
