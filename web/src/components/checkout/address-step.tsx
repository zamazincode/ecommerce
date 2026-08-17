"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { useQuery, useMutation } from "@tanstack/react-query";
import { apiFetch, ApiError } from "@/lib/api/client";
import { useCart } from "@/hooks/use-cart";
import { Button } from "@/components/ui/button";
import type { components } from "@/types/api";

type AddressDto = components["schemas"]["AddressDto"];
type OrderDetailDto = components["schemas"]["OrderDetailDto"];

export function AddressStep() {
	const router = useRouter();
	const { data: cart } = useCart();
	const [selectedAddressId, setSelectedAddressId] = useState<number | null>(
		null,
	);

	const { data: addresses } = useQuery({
		queryKey: ["addresses"],
		queryFn: () => apiFetch<AddressDto[]>("addresses"),
	});

	const createOrder = useMutation({
		mutationFn: (addressId: number) =>
			apiFetch<OrderDetailDto>("orders", {
				method: "POST",
				body: JSON.stringify({
					addressId,
					// ExpectedTotal: kullanıcının EKRANDA GÖRDÜĞÜ toplam. Sunucu bunu
					// kendi hesabıyla karşılaştırıyor; tutmazsa 409 — sepet ile ödeme
					// sayfası arasında fiyat/kupon/stok değişmiş demektir, kullanıcıya
					// "sepetiniz güncellendi, kontrol edin" gösterilmeli.
					expectedTotal: cart?.total,
				}),
			}),
		onSuccess: (order) => {
			router.push(`/odeme?step=payment&siparis=${order.orderNumber}`);
		},
		onError: (error) => {
			if (error instanceof ApiError && error.status === 409) {
				// TODO: "Sepetiniz değişti, lütfen tekrar kontrol edin" göster,
				// sepet sorgusunu invalidate et.
			}
		},
	});

	return (
		<div>
			<h1 className="mb-4 text-lg font-semibold">Teslimat Adresi</h1>

			<div className="space-y-2">
				{addresses?.map((address) => (
					<label key={address.id} className="flex items-center gap-2">
						<input
							type="radio"
							name="address"
							checked={selectedAddressId === address.id}
							onChange={() =>
								setSelectedAddressId(address.id as number)
							}
						/>
						{address.title} — {address.fullAddress},{" "}
						{address.district}/{address.city}
					</label>
				))}
			</div>

			<Button
				className="mt-6"
				disabled={!selectedAddressId || createOrder.isPending}
				onClick={() =>
					selectedAddressId && createOrder.mutate(selectedAddressId)
				}
			>
				{createOrder.isPending ? "Sipariş oluşturuluyor…" : "Devam Et"}
			</Button>
		</div>
	);
}
