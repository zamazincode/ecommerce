"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { apiFetch } from "@/lib/api/client";
import { queryKeys } from "@/lib/api/query-keys";
import type { components } from "@/types/api";

type AdminCouponDto = components["schemas"]["AdminCouponDto"];
type CreateCouponRequest = components["schemas"]["CreateCouponRequest"];

export function useAdminCoupons() {
	return useQuery({
		queryKey: queryKeys.adminCoupons,
		queryFn: () => apiFetch<AdminCouponDto[]>("admin/coupons"),
	});
}

export function useCreateCoupon() {
	const queryClient = useQueryClient();
	return useMutation({
		mutationFn: (input: CreateCouponRequest) =>
			apiFetch<AdminCouponDto>("admin/coupons", {
				method: "POST",
				body: JSON.stringify(input),
			}),
		onSuccess: () =>
			queryClient.invalidateQueries({ queryKey: queryKeys.adminCoupons }),
	});
}

export function useSetCouponActive() {
	const queryClient = useQueryClient();
	return useMutation({
		mutationFn: ({ id, isActive }: { id: number; isActive: boolean }) =>
			apiFetch<AdminCouponDto>(`admin/coupons/${id}`, {
				method: "PATCH",
				body: JSON.stringify({ isActive }),
			}),
		onSuccess: () =>
			queryClient.invalidateQueries({ queryKey: queryKeys.adminCoupons }),
	});
}
