"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { apiFetch } from "@/lib/api/client";
import { queryKeys } from "@/lib/api/query-keys";
import type { components } from "@/types/api";

type CartDto = components["schemas"]["CartDto"];

export function useCart() {
	return useQuery({
		queryKey: queryKeys.cart,
		queryFn: () => apiFetch<CartDto>("cart"),
	});
}

export function useAddToCart() {
	const queryClient = useQueryClient();

	return useMutation({
		mutationFn: (input: { productId: number; quantity: number }) =>
			apiFetch<CartDto>("cart/items", {
				method: "POST",
				body: JSON.stringify(input),
			}),

		// OPTIMISTIC UPDATE: sunucu cevabını beklemeden UI'ı güncelle.
		onMutate: async (input) => {
			await queryClient.cancelQueries({ queryKey: queryKeys.cart });
			const previousCart = queryClient.getQueryData<CartDto>(
				queryKeys.cart,
			);

			queryClient.setQueryData<CartDto>(queryKeys.cart, (old) => {
				if (!old) return old;
				const existing = old.items.find(
					(i) => i.productId === input.productId,
				);

				if (existing) {
					// Aynı ürün zaten sepette — adet artıyor, yeni satır oluşmuyor.
					return {
						...old,
						items: old.items.map((i) =>
							i.productId === input.productId
								? {
										...i,
										quantity: ((i.quantity as number) +
											input.quantity) as number,
									}
								: i,
						),
					};
				}

				// Yeni satır — fiyat/görsel bilgisi sunucudan gelmediği için burada
				// TAHMİNİ bir satır ekleyemeyiz (ürün adı/görseli bilinmiyor).
				// Bu durumda optimistic update'i atla, sadece loading göster.
				return old;
			});

			return { previousCart };
		},

		onError: (_err, _input, context) => {
			// Geri al — sunucu reddetti (stok yetersiz, ürün pasif vb.)
			if (context?.previousCart) {
				queryClient.setQueryData(queryKeys.cart, context.previousCart);
			}
		},

		onSettled: () => {
			// Başarılı da olsa hata da olsa sunucudaki GERÇEK hâli çek —
			// optimistic tahminimiz (adet artışı) sunucunun kırptığı/uyardığı
			// durumları yansıtmıyor olabilir.
			queryClient.invalidateQueries({ queryKey: queryKeys.cart });
		},
	});
}

export function useUpdateCartItem() {
	const queryClient = useQueryClient();

	return useMutation({
		mutationFn: (input: { productId: number; quantity: number }) =>
			apiFetch<CartDto>(`cart/items/${input.productId}`, {
				method: "PATCH",
				body: JSON.stringify({ quantity: input.quantity }),
			}),
		onSettled: () =>
			queryClient.invalidateQueries({ queryKey: queryKeys.cart }),
	});
}

export function useRemoveCartItem() {
	const queryClient = useQueryClient();

	return useMutation({
		mutationFn: (productId: number) =>
			apiFetch<CartDto>(`cart/items/${productId}`, { method: "DELETE" }),

		onMutate: async (productId) => {
			await queryClient.cancelQueries({ queryKey: queryKeys.cart });
			const previousCart = queryClient.getQueryData<CartDto>(
				queryKeys.cart,
			);

			queryClient.setQueryData<CartDto>(queryKeys.cart, (old) =>
				old
					? {
							...old,
							items: old.items.filter(
								(i) => i.productId !== productId,
							),
						}
					: old,
			);

			return { previousCart };
		},
		onError: (_err, _productId, context) => {
			if (context?.previousCart)
				queryClient.setQueryData(queryKeys.cart, context.previousCart);
		},
		onSettled: () =>
			queryClient.invalidateQueries({ queryKey: queryKeys.cart }),
	});
}

export function useApplyCoupon() {
	const queryClient = useQueryClient();

	return useMutation({
		mutationFn: (code: string) =>
			apiFetch<CartDto>("cart/coupon", {
				method: "POST",
				body: JSON.stringify({ code }),
			}),
		onSuccess: (cart) => queryClient.setQueryData(queryKeys.cart, cart),
	});
}

export function useRemoveCoupon() {
	const queryClient = useQueryClient();

	return useMutation({
		mutationFn: () =>
			apiFetch<CartDto>("cart/coupon", { method: "DELETE" }),
		onSuccess: (cart) => queryClient.setQueryData(queryKeys.cart, cart),
	});
}

export function useClearCart() {
	const queryClient = useQueryClient();
	return useMutation({
		mutationFn: () => apiFetch<CartDto>("cart", { method: "DELETE" }),
		onSuccess: (cart) => queryClient.setQueryData(queryKeys.cart, cart),
	});
}

export function useMergeCart() {
	const queryClient = useQueryClient();

	return useMutation({
		mutationFn: () => apiFetch<CartDto>("cart/merge", { method: "POST" }),
		onSuccess: (cart) => {
			queryClient.setQueryData(queryKeys.cart, cart);
			// Birleştirme bitti, misafir kimliğine artık gerek yok.
			import("@/lib/guest-id").then((m) => m.clearGuestId());
		},
	});
}
