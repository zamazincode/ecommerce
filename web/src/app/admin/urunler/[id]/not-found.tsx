import Link from "next/link";
import { BookOpenIcon } from "lucide-react";
import { Button } from "@/components/ui/button";
import { EmptyState } from "@/components/ui/empty-state";

export default function ProductNotFoundAdmin() {
	return (
		<div className="flex min-h-[50vh] items-center justify-center py-16">
			<EmptyState
				icon={BookOpenIcon}
				title="Ürün bulunamadı"
				description="Bu id'ye ait bir ürün yok — silinmiş ya da hiç var olmamış olabilir."
				action={
					<Button
						render={<Link href="/admin/urunler" />}
						nativeButton={false}
					>
						Ürünlere Dön
					</Button>
				}
			/>
		</div>
	);
}
