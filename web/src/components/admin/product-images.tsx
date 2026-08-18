"use client";

import { useRef, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { apiFetch } from "@/lib/api/client";
import { Button } from "@/components/ui/button";
import { toast } from "@/components/ui/toast";
import { XIcon } from "lucide-react";
import type { components } from "@/types/api";

type SignedUploadDto = components["schemas"]["SignedUploadDto"];
type ProductImageDto = components["schemas"]["ProductImageDto"];

async function uploadToCloudinary(
	file: File,
	signed: SignedUploadDto,
): Promise<{ public_id: string }> {
	// SADECE imzalanan alanlar + dosya + api_key gönderilir. Fazladan bir
	// alan (ör. "tags") eklersen Cloudinary imzayı GEÇERSİZ sayar — sunucu
	// yalnızca folder+timestamp'i imzaladı (CloudinaryImageStorage.cs).
	const form = new FormData();
	form.set("file", file);
	form.set("api_key", signed.apiKey);
	form.set("timestamp", signed.timestamp);
	form.set("signature", signed.signature);
	form.set("folder", signed.folder);

	// DİKKAT: bu istek proxy'den GEÇMİYOR — tarayıcı doğrudan Cloudinary'ye
	// gidiyor. `signed.url` Development'ta gerçek Cloudinary anahtarı yoksa
	// `https://fake-upload.test/...` gibi ÇÖZÜLEMEYEN bir adres olabilir
	// (FakeImageStorage.cs) — bu durumda fetch bir ağ hatasıyla reddeder,
	// aşağıdaki catch bunu yakalar.
	const response = await fetch(signed.url, { method: "POST", body: form });
	if (!response.ok) throw new Error("Cloudinary yükleme reddetti.");
	return response.json();
}

export function ProductImages({ productId }: { productId: number }) {
	const queryClient = useQueryClient();
	const fileInputRef = useRef<HTMLInputElement>(null);
	const [isUploading, setIsUploading] = useState(false);

	const imagesKey = ["admin", "product-images", productId] as const;

	const { data: images } = useQuery({
		queryKey: imagesKey,
		queryFn: () =>
			apiFetch<ProductImageDto[]>(`admin/products/${productId}/images`),
	});

	const upload = useMutation({
		mutationFn: async (file: File) => {
			setIsUploading(true);
			try {
				const signed = await apiFetch<SignedUploadDto>(
					"admin/images/signature",
					{ method: "POST" },
				);
				const { public_id } = await uploadToCloudinary(file, signed);
				return apiFetch<ProductImageDto>(
					`admin/products/${productId}/images`,
					{
						method: "POST",
						body: JSON.stringify({ publicId: public_id }),
					},
				);
			} finally {
				setIsUploading(false);
			}
		},
		onSuccess: () => {
			toast.add({ title: "Görsel eklendi", type: "success" });
			queryClient.invalidateQueries({ queryKey: imagesKey });
		},
		onError: () => {
			toast.add({
				title: "Görsel yüklenemedi",
				description:
					"Cloudinary'ye ulaşılamadı — Development ortamında gerçek Cloudinary anahtarı yoksa bu beklenen bir durumdur.",
				type: "error",
			});
		},
	});

	const deleteImage = useMutation({
		mutationFn: (imageId: number) =>
			apiFetch(`admin/product-images/${imageId}`, { method: "DELETE" }),
		onSuccess: () => {
			queryClient.invalidateQueries({ queryKey: imagesKey });
		},
	});

	return (
		<div>
			<h2 className="mb-2 font-medium">Görseller</h2>

			{images && images.length > 0 ? (
				<div className="mb-4 grid grid-cols-3 gap-2">
					{images.map((image) => (
						<div key={image.id as number} className="group relative">
							{/* eslint-disable-next-line @next/next/no-img-element -- Cloudinary/D&R URL'i, sabit boyutlu küçük bir küme; next/image'ın optimizasyon maliyetine değmez */}
							<img
								src={image.url}
								alt=""
								className="aspect-2/3 w-full rounded object-cover"
							/>
							<button
								onClick={() => {
									if (confirm("Bu görsel silinsin mi?"))
										deleteImage.mutate(image.id as number);
								}}
								className="absolute top-1 right-1 rounded-full bg-background/80 p-1 opacity-0 group-hover:opacity-100"
								aria-label="Görseli sil"
							>
								<XIcon className="size-3" />
							</button>
						</div>
					))}
				</div>
			) : null}

			<input
				ref={fileInputRef}
				type="file"
				accept="image/*"
				className="hidden"
				onChange={(e) => {
					const file = e.target.files?.[0];
					if (file) upload.mutate(file);
					e.target.value = ""; // aynı dosyayı arka arkaya seçebilmek için
				}}
			/>
			<Button
				type="button"
				variant="outline"
				disabled={isUploading}
				onClick={() => fileInputRef.current?.click()}
			>
				{isUploading ? "Yükleniyor…" : "Görsel Yükle"}
			</Button>
		</div>
	);
}
