import { z } from "zod";

export const productFormSchema = z
	.object({
		name: z.string().min(1, "Ad gerekli.").max(300),
		sku: z.string().max(64).optional().or(z.literal("")),
		description: z.string().max(8000).optional().or(z.literal("")),
		price: z.coerce.number().positive("Fiyat 0'dan büyük olmalı."),
		discountedPrice: z.coerce
			.number()
			.positive()
			.optional()
			.or(z.literal("")),
		stock: z.coerce.number().int().min(0, "Stok negatif olamaz."),
		categoryId: z.coerce.number().int().positive("Kategori seçmelisin."),
		publisherId: z.coerce
			.number()
			.int()
			.positive()
			.optional()
			.or(z.literal("")),
		brandId: z.coerce
			.number()
			.int()
			.positive()
			.optional()
			.or(z.literal("")),
		isActive: z.boolean(),
	})
	.refine(
		(data) => !data.discountedPrice || data.discountedPrice < data.price,
		{
			message: "İndirimli fiyat, normal fiyattan düşük olmalı.",
			path: ["discountedPrice"],
		},
	);

export type ProductFormInput = z.infer<typeof productFormSchema>;
