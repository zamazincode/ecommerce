import { expect, test } from "vitest";
import { loginSchema, registerSchema } from "@/lib/validations/auth";

test("loginSchema geçersiz e-postayı reddeder", () => {
	const result = loginSchema.safeParse({ email: "gecersiz", password: "x" });
	expect(result.success).toBe(false);
});

test("registerSchema zayıf şifreyi reddeder", () => {
	const result = registerSchema.safeParse({
		email: "test@example.com",
		password: "kisa",
		firstName: "Test",
		lastName: "Kullanıcı",
	});
	expect(result.success).toBe(false);
});

test("registerSchema geçerli veriyi kabul eder", () => {
	const result = registerSchema.safeParse({
		email: "test@example.com",
		password: "Test1234",
		firstName: "Test",
		lastName: "Kullanıcı",
	});
	expect(result.success).toBe(true);
});
