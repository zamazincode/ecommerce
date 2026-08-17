import { describe, expect, test, vi, beforeEach } from "vitest";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { renderHook, waitFor } from "@testing-library/react";
import { act } from "react";
import { useAddToCart, useRemoveCartItem } from "@/hooks/use-cart";
import { queryKeys } from "@/lib/api/query-keys";
import type { components } from "@/types/api";

type CartDto = components["schemas"]["CartDto"];

const baseCart: CartDto = {
	items: [
		{
			productId: 1,
			name: "Test Kitap",
			slug: "test-kitap",
			imageUrl: null,
			unitPrice: 100,
			quantity: 1,
			lineTotal: 100,
			availableStock: 5,
			priceChanged: false,
		},
	],
	couponCode: null,
	subTotal: 100,
	discountAmount: 0,
	shippingCost: 0,
	total: 100,
	freeShippingRemaining: 0,
	warnings: [],
	totalQuantity: 1,
};

function createWrapper() {
	const queryClient = new QueryClient({
		defaultOptions: {
			queries: { retry: false },
			mutations: { retry: false },
		},
	});
	queryClient.setQueryData(queryKeys.cart, baseCart);
	// eslint-disable-next-line react/display-name
	return {
		queryClient,
		wrapper: ({ children }: { children: React.ReactNode }) => (
			<QueryClientProvider client={queryClient}>
				{children}
			</QueryClientProvider>
		),
	};
}

beforeEach(() => {
	vi.restoreAllMocks();
});

test("aynı ürünü tekrar eklerken UI, sunucu cevabını beklemeden adedi artırır", async () => {
	global.fetch = vi.fn().mockImplementation(
		() => new Promise(() => {}), // bilerek hiç çözülmüyor — optimistic anı yakalıyoruz
	);
	const { queryClient, wrapper } = createWrapper();
	const { result } = renderHook(() => useAddToCart(), { wrapper });

	act(() => {
		result.current.mutate({ productId: 1, quantity: 1 });
	});

	await waitFor(() => {
		const cart = queryClient.getQueryData<CartDto>(queryKeys.cart);
		expect(cart?.items[0].quantity).toBe(2);
	});
});

test("sunucu reddederse (409) optimistic değişiklik geri alınır", async () => {
	global.fetch = vi.fn().mockResolvedValue({
		ok: false,
		status: 400,
		json: () => Promise.resolve({ message: "Stokta yeterli ürün yok" }),
	});
	const { queryClient, wrapper } = createWrapper();
	const { result } = renderHook(() => useRemoveCartItem(), { wrapper });

	act(() => {
		result.current.mutate(1);
	});

	await waitFor(() => expect(result.current.isError).toBe(true));

	const cart = queryClient.getQueryData<CartDto>(queryKeys.cart);
	expect(cart?.items).toHaveLength(1); // geri geldi
});
