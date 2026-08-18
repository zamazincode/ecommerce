import { describe, expect, test } from "vitest";
import { ORDER_STATUS_LABELS, ORDER_STATUS_TRANSITIONS } from "@/lib/enums";

describe("ORDER_STATUS_TRANSITIONS", () => {
	test("terminal durumların (Delivered/Cancelled/Refunded) çıkışı yok", () => {
		expect(ORDER_STATUS_TRANSITIONS[4]).toEqual([]);
		expect(ORDER_STATUS_TRANSITIONS[5]).toEqual([]);
		expect(ORDER_STATUS_TRANSITIONS[6]).toEqual([]);
	});

	test("Delivered'dan Pending'e geçiş YOK", () => {
		expect(ORDER_STATUS_TRANSITIONS[4]).not.toContain(0);
	});

	test("her durumun bir Türkçe etiketi var", () => {
		for (const status of Object.keys(ORDER_STATUS_TRANSITIONS)) {
			expect(
				ORDER_STATUS_LABELS[Number(status) as keyof typeof ORDER_STATUS_LABELS],
			).toBeDefined();
		}
	});
});
