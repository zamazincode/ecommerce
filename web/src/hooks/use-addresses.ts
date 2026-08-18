"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { apiFetch } from "@/lib/api/client";
import type { components } from "@/types/api";

type AddressDto = components["schemas"]["AddressDto"];
type SaveAddressRequest = components["schemas"]["SaveAddressRequest"];

const key = ["addresses"] as const;

export function useAddresses() {
	return useQuery({
		queryKey: key,
		queryFn: () => apiFetch<AddressDto[]>("addresses"),
		meta: { errorMessage: "Adreslerin yüklenemedi." },
	});
}

export function useCreateAddress() {
	const queryClient = useQueryClient();
	return useMutation({
		mutationFn: (input: SaveAddressRequest) =>
			apiFetch<AddressDto>("addresses", {
				method: "POST",
				body: JSON.stringify(input),
			}),
		onSuccess: () => queryClient.invalidateQueries({ queryKey: key }),
	});
}

export function useUpdateAddress() {
	const queryClient = useQueryClient();
	return useMutation({
		mutationFn: ({
			id,
			input,
		}: {
			id: number;
			input: SaveAddressRequest;
		}) =>
			apiFetch<AddressDto>(`addresses/${id}`, {
				method: "PUT",
				body: JSON.stringify(input),
			}),
		onSuccess: () => queryClient.invalidateQueries({ queryKey: key }),
	});
}

export function useDeleteAddress() {
	const queryClient = useQueryClient();
	return useMutation({
		mutationFn: (id: number) =>
			apiFetch(`addresses/${id}`, { method: "DELETE" }),
		onSuccess: () => queryClient.invalidateQueries({ queryKey: key }),
	});
}
