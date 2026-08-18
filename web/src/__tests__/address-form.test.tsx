import { describe, expect, test } from "vitest";
import { addressFormSchema } from "@/lib/validations/address";

test("geçerli bir Türkiye telefon numarası kabul edilir", () => {
	const result = addressFormSchema.safeParse({
		title: "Ev",
		fullName: "Ahmet Yılmaz",
		phone: "+90 (532) 123-4567",
		city: "İstanbul",
		district: "Kadıköy",
		fullAddress: "Örnek mahalle örnek sokak no:1",
		isDefault: false,
	});
	expect(result.success).toBe(true);
});

test("harf içeren telefon numarası reddedilir", () => {
	const result = addressFormSchema.safeParse({
		title: "Ev",
		fullName: "Ahmet Yılmaz",
		phone: "0532-ABC-4567",
		city: "İstanbul",
		district: "Kadıköy",
		fullAddress: "Örnek mahalle örnek sokak no:1",
		isDefault: false,
	});
	expect(result.success).toBe(false);
});
