"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { ArrowRightIcon, MapPinIcon } from "lucide-react";
import { apiFetch, ApiError } from "@/lib/api/client";
import { useCart } from "@/hooks/use-cart";
import { useAddresses } from "@/hooks/use-addresses";
import { queryKeys } from "@/lib/api/query-keys";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { RadioGroup, RadioGroupItem } from "@/components/ui/radio-group";
import { EmptyState } from "@/components/ui/empty-state";
import { AddressFormDialog } from "@/components/account/address-form-dialog";
import { toast } from "@/components/ui/toast";
import type { components } from "@/types/api";

type OrderDetailDto = components["schemas"]["OrderDetailDto"];

export function AddressStep() {
	const router = useRouter();
	const queryClient = useQueryClient();
	const { data: cart } = useCart();
	const [selectedAddressId, setSelectedAddressId] = useState<number | null>(
		null,
	);

	const { data: addresses } = useAddresses();

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
			<h1 className="mb-4 font-heading text-lg font-semibold">
				Teslimat Adresi
			</h1>

			{addresses?.length === 0 ? (
				<EmptyState
					icon={MapPinIcon}
					title="Henüz kayıtlı adresin yok"
					description="Sipariş verebilmek için en az bir adres eklemelisin."
					action={
						<AddressFormDialog trigger={<Button>Adres Ekle</Button>} />
					}
				/>
			) : (
				<>
					<RadioGroup
						value={selectedAddressId}
						onValueChange={(value) =>
							setSelectedAddressId(value as number)
						}
						className="space-y-3"
					>
						{addresses?.map((address) => (
							<div
								key={address.id}
								onClick={() =>
									setSelectedAddressId(address.id as number)
								}
								className="flex cursor-pointer items-start gap-3 rounded-xl border p-4 transition-colors has-data-checked:border-primary has-data-checked:bg-primary-soft"
							>
								<RadioGroupItem
									value={address.id as number}
									className="mt-0.5"
								/>
								<div className="min-w-0 flex-1 text-sm">
									<div className="flex flex-wrap items-center gap-2">
										<span className="font-medium">
											{address.title}
										</span>
										{address.isDefault ? (
											<Badge variant="brand-soft">
												Varsayılan
											</Badge>
										) : null}
									</div>
									<p className="mt-1 text-muted-foreground">
										{address.fullName} · {address.phone}
									</p>
									<p className="text-muted-foreground">
										{address.fullAddress},{" "}
										{address.district}/{address.city}
									</p>
								</div>
							</div>
						))}
					</RadioGroup>

					<div className="mt-4">
						<AddressFormDialog
							trigger={
								<Button variant="outline" size="sm">
									Yeni Adres Ekle
								</Button>
							}
						/>
					</div>

					<Button
						size="lg"
						className="mt-6 w-full sm:w-auto"
						disabled={!selectedAddressId || createOrder.isPending}
						onClick={() =>
							selectedAddressId &&
							createOrder.mutate(selectedAddressId)
						}
					>
						{createOrder.isPending
							? "Sipariş oluşturuluyor…"
							: "Devam Et"}
						<ArrowRightIcon />
					</Button>
				</>
			)}
		</div>
	);
}
