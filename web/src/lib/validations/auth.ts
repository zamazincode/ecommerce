import z, { email } from "zod";

export const loginSchema = z.object({
	email: z.email("Geçerli bir e-posta adresi girin."),
	password: z.string().min(1, "Şifre Gerekli"),
});

export const registerSchema = z.object({
	email: z.email("Geçerli bir e-posta adresi girin."),
	password: z
		.string()
		.min(8, "Şifre en az 8 karakter olmalı.")
		.regex(/[A-Z]/, "En az bir büyük harf içermeli.")
		.regex(/[a-z]/, "En az bir küçük harf içermeli.")
		.regex(/[0-9]/, "En az bir rakam içermeli."),
	firstName: z.string().min(1, "Ad gerekli."),
	lastName: z.string().min(1, "Soyad gerekli."),
});

export type LoginInput = z.infer<typeof loginSchema>;
export type RegisterInput = z.infer<typeof registerSchema>;
