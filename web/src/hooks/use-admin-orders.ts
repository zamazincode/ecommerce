"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { apiFetch, ApiError } from "@/lib/api/client";
import { queryKeys } from "@/lib/api/query-keys";
import type { AdminOrderFilters } from "@/types";
import type { components } from "@/types/api";

type PagedResultOfAdminOrderListDto =
	components["schemas"]["PagedResultOfAdminOrderListDto"];
type OrderStatus = components["schemas"]["OrderStatus"];

export type { AdminOrderFilters };

export function useAdminOrders(filters: AdminOrderFilters) {
	const params = new URLSearchParams();
	for (const [key, value] of Object.entries(filters)) {
		if (value !== undefined && value !== "") params.set(key, String(value));
	}

	return useQuery({
		queryKey: queryKeys.adminOrders(filters),
		queryFn: () =>
			apiFetch<PagedResultOfAdminOrderListDto>(
				`admin/orders?${params.toString()}`,
			),
	});
}

export function useUpdateOrderStatus() {
	const queryClient = useQueryClient();

	return useMutation({
		mutationFn: ({
			orderNumber,
			status,
		}: {
			orderNumber: string;
			status: OrderStatus;
		}) =>
			apiFetch(`admin/orders/${orderNumber}/status`, {
				method: "PATCH",
				body: JSON.stringify({ status }),
			}),
		onSuccess: () =>
			queryClient.invalidateQueries({ queryKey: ["admin", "orders"] }),
	});
}

export { ApiError };
