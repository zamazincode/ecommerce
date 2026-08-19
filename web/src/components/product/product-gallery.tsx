"use client";

import { useState } from "react";
import Image from "next/image";
import { cn } from "@/lib/utils";

/**
 * Ürün detayının ana görsel + thumbnail şeridi. `imageUrls` tek elemanlıysa
 * (kitap dışı 154 ürünün çoğu) thumbnail şeridi hiç gösterilmez.
 */
export function ProductGallery({
	images,
	productName,
}: {
	images: string[];
	productName: string;
}) {
	const [activeIndex, setActiveIndex] = useState(0);
	const activeImage = images[activeIndex];

	return (
		<div className="space-y-3">
			<div className="relative aspect-3/4 overflow-hidden rounded-2xl bg-muted/40 p-6">
				{activeImage ? (
					<Image
						src={activeImage}
						alt={productName}
						fill
						priority
						sizes="(max-width: 1024px) 90vw, 420px"
						className="object-contain"
					/>
				) : null}
			</div>

			{images.length > 1 ? (
				<div className="flex gap-2 overflow-x-auto">
					{images.map((image, index) => (
						<button
							key={image}
							type="button"
							onClick={() => setActiveIndex(index)}
							aria-label={`${index + 1}. görseli göster`}
							aria-current={index === activeIndex}
							className={cn(
								"relative aspect-3/4 w-16 shrink-0 overflow-hidden rounded-lg bg-muted/40 ring-1 transition-all",
								index === activeIndex
									? "ring-2 ring-primary"
									: "ring-border hover:ring-primary/40",
							)}
						>
							<Image
								src={image}
								alt=""
								fill
								sizes="64px"
								className="object-contain p-1"
							/>
						</button>
					))}
				</div>
			) : null}
		</div>
	);
}
