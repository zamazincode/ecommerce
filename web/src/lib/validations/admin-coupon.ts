import { z } from "zod";

// .NET'teki CreateCouponRequestValidator ile birebir.
export const couponFormSchema = z
	.object({
		code: z
			.string()
			.min(3)
			.max(50)
			.regex(
				/^[A-Z0-9_-]+$/i,
				"Kupon kodu yalnızca harf, rakam, '_' ve '-' içerebilir.",
			),
		type: z.coerce.number().int().min(0).max(1),
		value: z.coerce.number().positive("Değer 0'dan büyük olmalı."),
		minCartTotal: z.coerce.number().min(0),
		validFrom: z.string().min(1, "Başlangıç tarihi gerekli."),
		validTo: z.string().min(1, "Bitiş tarihi gerekli."),
		usageLimit: z.coerce
			.number()
			.int()
			.positive()
			.optional()
			.or(z.literal("")),
	})
	.refine((data) => data.validTo > data.validFrom, {
		message: "Bitiş tarihi başlangıçtan sonra olmalı.",
		path: ["validTo"],
	})
	.refine((data) => data.type !== 0 || data.value <= 100, {
		message: "Yüzdesel kupon değeri 100'ü geçemez.",
		path: ["value"],
	});

export type CouponFormInput = z.infer<typeof couponFormSchema>;
// zod'un `z.coerce` alanları RAW (girdi) ile PARSED (çıktı) tipini ayırıyor —
// bkz. product-form.tsx'teki aynı not.
export type CouponFormRawValues = z.input<typeof couponFormSchema>;
