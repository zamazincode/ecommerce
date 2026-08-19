"use client";

import { PlusIcon } from "lucide-react";
import { Button } from "@/components/ui/button";
import { useAddToCart } from "@/hooks/use-cart";
import { useUiStore } from "@/stores/ui-store";
import { toast } from "@/components/ui/toast";
import { ApiError } from "@/lib/api/client";

/**
 * Ürün kartının sağ alt köşesindeki "hızlı sepete ekle" butonu — dr.com.tr
 * desenindeki tek tık ekleme. `favorite-button.tsx`'teki gibi kartın kendi
 * `<Link>`'ini tetiklemesin diye tıklama olayı durduruluyor.
 */
export function QuickAddButton({
	productId,
	productName,
	inStock,
}: {
	productId: number;
	productName: string;
	inStock: boolean;
}) {
	const addToCart = useAddToCart();
	const openCartPanel = useUiStore((s) => s.openCartPanel);

	if (!inStock) return null;

	async function handleClick(e: React.MouseEvent) {
		e.preventDefault();
		e.stopPropagation();

		try {
			await addToCart.mutateAsync({ productId, quantity: 1 });
			openCartPanel();
		} catch (error) {
			toast.add({
				title:
					error instanceof ApiError &&
					error.body &&
					typeof error.body === "object" &&
					"detail" in error.body
						? String(error.body.detail)
						: "Sepete eklenemedi.",
				type: "error",
			});
		}
	}

	return (
		<Button
			type="button"
			size="icon"
			shape="pill"
			variant="accent"
			onClick={handleClick}
			disabled={addToCart.isPending}
			aria-label={`${productName} ürününü sepete ekle`}
			className="absolute right-3 bottom-3 opacity-0 shadow-card transition-opacity focus-visible:opacity-100 group-hover:opacity-100 max-md:opacity-100 md:transition-opacity"
		>
			<PlusIcon />
		</Button>
	);
}
