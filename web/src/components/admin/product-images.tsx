"use client";

import { useRef, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { apiFetch } from "@/lib/api/client";
import { Card, CardAction, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { ConfirmDialog } from "@/components/ui/confirm-dialog";
import { Skeleton } from "@/components/ui/skeleton";
import { toast } from "@/components/ui/toast";
import { ImageIcon, LoaderIcon, XIcon } from "lucide-react";
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

	const hasImages = !!images && images.length > 0;

	return (
		<Card>
			<CardHeader>
				<CardTitle>Görseller</CardTitle>
				{hasImages ? (
					<CardAction>
						<Button
							type="button"
							variant="outline"
							size="sm"
							disabled={isUploading}
							onClick={() => fileInputRef.current?.click()}
						>
							{isUploading ? "Yükleniyor…" : "Görsel Yükle"}
						</Button>
					</CardAction>
				) : null}
			</CardHeader>
			<CardContent>
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

				{hasImages ? (
					<div className="grid grid-cols-3 gap-3">
						{images.map((image) => (
							<div
								key={image.id as number}
								className="group relative aspect-2/3 overflow-hidden rounded-xl bg-muted/40 ring-1 ring-border"
							>
								{/* eslint-disable-next-line @next/next/no-img-element -- Cloudinary/D&R URL'i, sabit boyutlu küçük bir küme; next/image'ın optimizasyon maliyetine değmez */}
								<img
									src={image.url}
									alt={`Ürün görseli ${Number(image.displayOrder) + 1}`}
									className="size-full object-contain p-1"
								/>
								<ConfirmDialog
									trigger={
										<button
											type="button"
											aria-label="Görseli sil"
											className="absolute top-1 right-1 rounded-full bg-background/80 p-1 opacity-0 transition-opacity group-hover:opacity-100 max-md:opacity-100"
										>
											<XIcon className="size-3" />
										</button>
									}
									title="Bu görsel silinsin mi?"
									tone="danger"
									confirmLabel="Sil"
									onConfirm={() =>
										deleteImage.mutate(image.id as number)
									}
								/>
							</div>
						))}
						{isUploading ? (
							<div className="relative flex aspect-2/3 items-center justify-center overflow-hidden rounded-xl bg-muted/40 ring-1 ring-border">
								<Skeleton className="absolute inset-0 rounded-none" />
								<LoaderIcon className="relative size-5 animate-spin text-muted-foreground" />
							</div>
						) : null}
					</div>
				) : (
					<button
						type="button"
						disabled={isUploading}
						onClick={() => fileInputRef.current?.click()}
						className="flex w-full flex-col items-center gap-2 rounded-xl border-2 border-dashed p-8 text-center text-sm text-muted-foreground hover:border-primary/40 hover:text-foreground disabled:pointer-events-none disabled:opacity-50"
					>
						{isUploading ? (
							<LoaderIcon className="size-6 animate-spin" />
						) : (
							<ImageIcon className="size-6" />
						)}
						<span>
							{isUploading
								? "Yükleniyor…"
								: "Bu ürünün henüz görseli yok"}
						</span>
						{!isUploading ? (
							<span className="font-medium text-primary">
								Görsel Yükle
							</span>
						) : null}
					</button>
				)}
			</CardContent>
		</Card>
	);
}
