import { expect, test, vi } from "vitest";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen } from "@testing-library/react";
import { ProductCard } from "@/components/product/product-card";
import type { ProductListDto } from "@/types";

vi.mock("next/navigation", () => ({
	useRouter: () => ({ push: vi.fn() }),
}));

const baseProduct: ProductListDto = {
	id: 1,
	slug: "suc-ve-ceza",
	name: "Suç ve Ceza",
	authorNames: "Fyodor Dostoyevski",
	price: 100,
	discountedPrice: null,
	effectivePrice: 100,
	inStock: true,
	imageUrl: "https://i.dr.com.tr/test.jpg",
	categoryId: 1,
};

function renderWithProviders(ui: React.ReactElement) {
	// FavoriteButton (ProductCard'ın içinde, Step 11) useSession/useFavoriteIds
	// için TanStack Query context'i istiyor.
	const queryClient = new QueryClient({
		defaultOptions: { queries: { retry: false } },
	});
	return render(
		<QueryClientProvider client={queryClient}>{ui}</QueryClientProvider>,
	);
}

test("indirimli fiyat varsa üstü çizili normal fiyat gösterilir", () => {
	renderWithProviders(
		<ProductCard
			product={{
				...baseProduct,
				discountedPrice: 80,
				effectivePrice: 80,
			}}
		/>,
	);

	expect(screen.getByText("₺80,00")).toBeDefined();
	expect(screen.getByText("₺100,00")).toBeDefined();
});

// test("indirim yoksa sadece tek fiyat gösterilir", () => {
// 	renderWithProviders(<ProductCard product={baseProduct} />);

// 	expect(screen.getByText("₺100,00")).toBeDefined();
// 	expect(screen.queryByText("line-through")).toBeNull();
// });

test("stokta yoksa uyarı gösterilir", () => {
	renderWithProviders(
		<ProductCard product={{ ...baseProduct, inStock: false }} />,
	);

	expect(screen.getByText("Stokta yok")).toBeDefined();
});
