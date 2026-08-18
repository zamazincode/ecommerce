"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { apiFetch } from "@/lib/api/client";
import { queryKeys } from "@/lib/api/query-keys";
import type { components } from "@/types/api";

type PagedResultOfAdminReviewDto =
	components["schemas"]["PagedResultOfAdminReviewDto"];

export function useAdminReviews(onlyPending: boolean) {
	return useQuery({
		queryKey: queryKeys.adminReviews({ onlyPending }),
		queryFn: () =>
			apiFetch<PagedResultOfAdminReviewDto>(
				`admin/reviews?onlyPending=${onlyPending}`,
			),
	});
}

export function useApproveReview() {
	const queryClient = useQueryClient();
	return useMutation({
		mutationFn: (id: number) =>
			apiFetch(`admin/reviews/${id}/approve`, { method: "PATCH" }),
		onSuccess: () =>
			queryClient.invalidateQueries({ queryKey: ["admin", "reviews"] }),
	});
}

export function useDeleteReview() {
	const queryClient = useQueryClient();
	return useMutation({
		mutationFn: (id: number) =>
			apiFetch(`admin/reviews/${id}`, { method: "DELETE" }),
		onSuccess: () =>
			queryClient.invalidateQueries({ queryKey: ["admin", "reviews"] }),
	});
}
