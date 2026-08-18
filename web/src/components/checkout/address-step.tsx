"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import {
	useQuery,
	useMutation,
	useQueryClient,
} from "@tanstack/react-query";
import { apiFetch, ApiError } from "@/lib/api/client";
import { useCart } from "@/hooks/use-cart";
import { queryKeys } from "@/lib/api/query-keys";
import { Button } from "@/components/ui/button";
import { toast } from "@/components/ui/toast";
import type { components } from "@/types/api";
import Link from "next/link";

type AddressDto = components["schemas"]["AddressDto"];
type OrderDetailDto = components["schemas"]["OrderDetailDto"];

export function AddressStep() {
	const router = useRouter();
	const queryClient = useQueryClient();
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
				// Sunucudaki toplam ekranda gösterilenle uyuşmuyor — sepet
				// checkout sırasında değişmiş (fiyat/kupon/stok). Sepeti
				// tazeleyip kullanıcıyı bilgilendiriyoruz, sipariş oluşmadı.
				queryClient.invalidateQueries({ queryKey: queryKeys.cart });
				toast.add({
					title: "Sepetiniz değişti",
					description:
						"Fiyat, kupon ya da stok güncellendi — lütfen sepetini kontrol edip tekrar dene.",
					type: "error",
				});
				return;
			}
			toast.add({
				title: "Sipariş oluşturulamadı",
				description: "Bir hata oluştu, lütfen tekrar dene.",
				type: "error",
			});
		},
	});

	return (
		<div>
			<h1 className="mb-4 text-lg font-semibold">Teslimat Adresi</h1>

			{addresses?.length === 0 ? (
				<div className="rounded-lg border border-dashed p-6 text-center">
					<p className="mb-3 text-sm text-muted-foreground">
						Henüz kayıtlı adresin yok.
					</p>
					<Button variant="outline">
						<Link href="/hesabim/adreslerim">Adres Ekle</Link>
					</Button>
				</div>
			) : (
				<div className="space-y-2">
					{addresses?.map((address) => (
						<label
							key={address.id}
							className="flex items-center gap-2"
						>
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
			)}

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
