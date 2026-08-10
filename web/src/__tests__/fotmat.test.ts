import { expect, test } from "vitest";
import { formatPrice } from "@/lib/format";

test("formatPrice should convert number to TR local currency", () => {
	expect(formatPrice(1234.5)).toBe("₺1.234,50");
	expect(formatPrice(0)).toBe("₺0,00");
	expect(formatPrice(29.9)).toBe("₺29,90");
});
