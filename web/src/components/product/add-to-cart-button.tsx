"use client";

import { useState } from "react";
import { useAddToCart } from "@/hooks/use-cart";
import { useUiStore } from "@/stores/ui-store";
import { Button } from "@/components/ui/button";
import { toast } from "@/components/ui/toast";
import { ApiError } from "@/lib/api/client";

export function AddToCartButton({
	productId,
	inStock,
}: {
	productId: number;
	inStock: boolean;
}) {
	const addToCart = useAddToCart();
	const openCartPanel = useUiStore((s) => s.openCartPanel);
	const [quantity, setQuantity] = useState(1);

	async function handleAdd() {
		try {
			await addToCart.mutateAsync({ productId, quantity });
			// Ekledikten sonra sepet panelini (6.6) AÇ — kullanıcı "eklendi mi
			// eklenmedi mi" belirsizliğinde kalmasın, anında görsün.
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
			<Button disabled className="w-full">
				Stokta Yok
			</Button>
		);
	}

	return (
		<div className="flex items-center gap-3">
			<input
				type="number"
				min={1}
				max={10}
				value={quantity}
				onChange={(e) =>
					setQuantity(Math.max(1, Number(e.target.value)))
				}
				className="w-16 rounded-md border px-2 py-2 text-center"
				aria-label="Adet"
			/>
			<Button
				onClick={handleAdd}
				disabled={addToCart.isPending}
				className="flex-1"
			>
				{addToCart.isPending ? "Ekleniyor…" : "Sepete Ekle"}
			</Button>
		</div>
	);
}
