"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { apiFetch } from "@/lib/api/client";
import { queryKeys } from "@/lib/api/query-keys";
import type { components } from "@/types/api";
import { AdminProductFilters } from "@/types";

type PagedResultOfAdminProductListDto =
	components["schemas"]["PagedResultOfAdminProductListDto"];
type BulkPriceUpdateItem = components["schemas"]["BulkPriceUpdateItem"];
type BulkPriceUpdateResult = components["schemas"]["BulkPriceUpdateResult"];

function toQueryString(filters: AdminProductFilters): string {
	const params = new URLSearchParams();
	for (const [key, value] of Object.entries(filters)) {
		if (value === undefined || value === "") continue;
		params.set(key, String(value));
	}
	const qs = params.toString();
	return qs ? `?${qs}` : "";
}

export function useAdminProducts(filters: AdminProductFilters) {
	return useQuery({
		queryKey: queryKeys.adminProducts(filters),
		queryFn: () =>
			apiFetch<PagedResultOfAdminProductListDto>(
				`admin/products${toQueryString(filters)}`,
			),
	});
}

export function useUpdateStock() {
	const queryClient = useQueryClient();

	return useMutation({
		mutationFn: ({ id, stock }: { id: number; stock: number }) =>
			apiFetch(`admin/products/${id}/stock`, {
				method: "PATCH",
				body: JSON.stringify({ stock }),
			}),
		// SATIR İÇİ DÜZENLEME: sunucu cevabını beklemeden tabloyu güncelle.
		onMutate: async ({ id, stock }) => {
			await queryClient.cancelQueries({
				queryKey: ["admin", "products"],
			});
			const previous =
				queryClient.getQueriesData<PagedResultOfAdminProductListDto>({
					queryKey: ["admin", "products"],
				});

			queryClient.setQueriesData<PagedResultOfAdminProductListDto>(
				{ queryKey: ["admin", "products"] },
				(old) =>
					old
						? {
								...old,
								items: old.items.map((p) =>
									p.id === id ? { ...p, stock } : p,
								),
							}
						: old,
			);

			return { previous };
		},
		onError: (_err, _vars, context) => {
			// 409 (xmin çakışması) dahil HER hata geri alınır; kullanıcı
			// "kaydedilmedi, tekrar dene" görür.
			context?.previous.forEach(([key, data]) =>
				queryClient.setQueryData(key, data),
			);
		},
		onSettled: () =>
			queryClient.invalidateQueries({ queryKey: ["admin", "products"] }),
	});
}

export function useDeleteProduct() {
	const queryClient = useQueryClient();
	return useMutation({
		mutationFn: (id: number) =>
			apiFetch(`admin/products/${id}`, { method: "DELETE" }),
		onSuccess: () =>
			queryClient.invalidateQueries({ queryKey: ["admin", "products"] }),
	});
}

export function useRestoreProduct() {
	const queryClient = useQueryClient();
	return useMutation({
		mutationFn: (id: number) =>
			apiFetch(`admin/products/${id}/restore`, { method: "POST" }),
		onSuccess: () =>
			queryClient.invalidateQueries({ queryKey: ["admin", "products"] }),
	});
}

export function useBulkUpdatePrice() {
	const queryClient = useQueryClient();
	return useMutation({
		mutationFn: (items: BulkPriceUpdateItem[]) =>
			apiFetch<BulkPriceUpdateResult>("admin/products/bulk-price", {
				method: "POST",
				body: JSON.stringify({ items }),
			}),
		onSuccess: () =>
			queryClient.invalidateQueries({ queryKey: ["admin", "products"] }),
	});
}
