"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { apiFetch } from "@/lib/api/client";
import { queryKeys } from "@/lib/api/query-keys";
import type { components } from "@/types/api";

type AdminCategoryDto = components["schemas"]["AdminCategoryDto"];
type CreateCategoryRequest = components["schemas"]["CreateCategoryRequest"];
type UpdateCategoryRequest = components["schemas"]["UpdateCategoryRequest"];

export function useAdminCategories() {
	return useQuery({
		queryKey: queryKeys.adminCategoriesFull,
		queryFn: () => apiFetch<AdminCategoryDto[]>("admin/categories"),
	});
}

export function useCreateCategory() {
	const queryClient = useQueryClient();
	return useMutation({
		mutationFn: (input: CreateCategoryRequest) =>
			apiFetch<AdminCategoryDto>("admin/categories", {
				method: "POST",
				body: JSON.stringify(input),
			}),
		onSuccess: () =>
			queryClient.invalidateQueries({
				queryKey: queryKeys.adminCategoriesFull,
			}),
	});
}

export function useUpdateCategory() {
	const queryClient = useQueryClient();
	return useMutation({
		mutationFn: ({
			id,
			input,
		}: {
			id: number;
			input: UpdateCategoryRequest;
		}) =>
			apiFetch<AdminCategoryDto>(`admin/categories/${id}`, {
				method: "PUT",
				body: JSON.stringify(input),
			}),
		onSuccess: () =>
			queryClient.invalidateQueries({
				queryKey: queryKeys.adminCategoriesFull,
			}),
	});
}
