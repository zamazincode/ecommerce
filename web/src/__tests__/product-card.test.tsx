import { expect, test } from "vitest";
import { render, screen } from "@testing-library/react";
import { ProductCard } from "@/components/product/product-card";
import type { ProductListDto } from "@/types";

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

test("indirimli fiyat varsa üstü çizili normal fiyat gösterilir", () => {
	render(
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
// 	render(<ProductCard product={baseProduct} />);

// 	expect(screen.getByText("₺100,00")).toBeDefined();
// 	expect(screen.queryByText("line-through")).toBeNull();
// });

test("stokta yoksa uyarı gösterilir", () => {
	render(<ProductCard product={{ ...baseProduct, inStock: false }} />);

	expect(screen.getByText("Stokta yok")).toBeDefined();
});
