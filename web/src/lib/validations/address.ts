import { z } from "zod";

// .NET SaveAddressRequestValidator ile birebir (OrderValidators.cs).
export const addressFormSchema = z.object({
	title: z.string().min(1, "Başlık gerekli.").max(50),
	fullName: z.string().min(1, "Ad soyad gerekli.").max(200),
	phone: z
		.string()
		.min(1, "Telefon gerekli.")
		.max(30)
		.regex(/^[0-9\s+\-()]+$/, "Telefon numarası geçersiz."),
	city: z.string().min(1, "İl gerekli.").max(100),
	district: z.string().min(1, "İlçe gerekli.").max(100),
	fullAddress: z
		.string()
		.min(10, "Adres en az 10 karakter olmalı.")
		.max(1000),
	isDefault: z.boolean(),
});
export type AddressFormInput = z.infer<typeof addressFormSchema>;
