"use client";

import { useState } from "react";
import { ShoppingCartIcon } from "lucide-react";
import { useAddToCart } from "@/hooks/use-cart";
import { useUiStore } from "@/stores/ui-store";
import { Button } from "@/components/ui/button";
import { QuantityStepper } from "@/components/ui/quantity-stepper";
import { toast } from "@/components/ui/toast";
import { ApiError } from "@/lib/api/client";

export function AddToCartButton({
	productId,
	inStock,
	stock,
}: {
	productId: number;
	inStock: boolean;
	/** Adet seçicinin üst sınırı — stoktan fazlası seçilemesin. */
	stock?: number;
}) {
	const addToCart = useAddToCart();
	const openCartPanel = useUiStore((s) => s.openCartPanel);
	const [quantity, setQuantity] = useState(1);

	async function handleAdd() {
		try {
			await addToCart.mutateAsync({ productId, quantity });
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

	if (!inStock) {
		return (
			<Button disabled size="lg" className="w-full">
				Stokta Yok
			</Button>
		);
	}

	const max = stock !== undefined ? Math.min(10, stock) : 10;

	return (
		<div className="flex items-center gap-3">
			<QuantityStepper value={quantity} max={max} onChange={setQuantity} />
			<Button
				size="lg"
				variant="accent"
				onClick={handleAdd}
				disabled={addToCart.isPending}
				className="flex-1"
			>
				<ShoppingCartIcon />
				{addToCart.isPending ? "Ekleniyor…" : "Sepete Ekle"}
			</Button>
		</div>
	);
}
