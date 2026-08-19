"use client";

import { useEffect, useState } from "react";
import Image from "next/image";
import Link from "next/link";
import { ArrowRightIcon } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { cn } from "@/lib/utils";
import type { ProductListDto } from "@/types";

// Metinler statik — dr.com.tr'deki gibi bir illüstrasyon setimiz yok, kullanıcı
// onayıyla sağ taraf katalogdan gelen gerçek kapaklarla dolduruluyor (aşağıda).
// Her slaytın kendi zemin tonu var — görsel varlığımız olmasa da slaytlar
// arasında geçiş "farklı bir görsele" geçmiş gibi hissettiriyor.
const SLIDES = [
	{
		badge: "Haftanın Fırsatı",
		title: "Okumaya Bugün Başla",
		description:
			"Binlerce kitap, kırtasiye ve hobi ürünü D&R'de seni bekliyor.",
		cta: "Alışverişe Başla",
		href: "/kategori/kitap",
		tone: "bg-primary-soft",
	},
	{
		badge: "Yeni Sezon",
		title: "Yeni Gelen Kitaplarla Tanış",
		description:
			"Rafa yeni giren kitapları keşfet, favorilerini hemen sepete ekle.",
		cta: "Yeni Gelenleri Gör",
		href: "/kategori/kitap",
		tone: "bg-warning-soft",
	},
	{
		badge: "Kampanya",
		title: "İndirimli Kitaplarda Fırsat Seni Bekliyor",
		description: "Seçili yüzlerce üründe büyük indirimler kaçmasın.",
		cta: "İndirimleri Keşfet",
		href: "/kategori/kitap",
		tone: "bg-red-soft",
	},
] as const;

// Kapakların hafif döndürülmüş, üst üste "fan" görünümü — plandaki
// `rotate-[-8deg] / rotate-3 / rotate-12` birebir.
const COVER_STYLES = [
	"-translate-x-16 rotate-[-8deg] z-0",
	"z-10 rotate-3",
	"translate-x-16 rotate-12 z-20",
];

/**
 * Anasayfa hero'su. Görsel varlığımız olmadığından sağ taraf CSS blob +
 * `home.bestsellers`'ın ilk 3 kapağıyla dolduruluyor — yeni asset gerekmez.
 * Metinler 6sn'de bir otomatik döner, `prefers-reduced-motion` ise durur.
 */
export function HeroSlider({ covers }: { covers: ProductListDto[] }) {
	const [index, setIndex] = useState(0);
	const coverImages = covers.filter((p) => p.imageUrl).slice(0, 3);

	useEffect(() => {
		if (SLIDES.length <= 1) return;
		if (
			typeof window !== "undefined" &&
			window.matchMedia("(prefers-reduced-motion: reduce)").matches
		) {
			return;
		}

		const id = setInterval(() => {
			setIndex((i) => (i + 1) % SLIDES.length);
		}, 6000);
		return () => clearInterval(id);
	}, []);

	const slide = SLIDES[index];

	return (
		<section className="container-x">
			<div
				className={cn(
					"grid items-center gap-8 overflow-hidden rounded-3xl px-6 py-10 transition-colors duration-700 md:grid-cols-2 md:px-12 md:py-14",
					slide.tone,
				)}
			>
				<div className="flex min-h-80 flex-col justify-between md:min-h-90">
					<div key={index} className="animate-in fade-in slide-in-from-bottom-3 duration-500">
						<Badge variant="accent">{slide.badge}</Badge>
						<h1 className="mt-4 font-heading text-3xl leading-tight font-bold text-primary md:text-5xl">
							{slide.title}
						</h1>
						<p className="mt-4 max-w-md text-muted-foreground">
							{slide.description}
						</p>
						<Button
							size="xl"
							shape="pill"
							className="mt-6"
							render={<Link href={slide.href} />}
							nativeButton={false}
						>
							{slide.cta}
							<ArrowRightIcon />
						</Button>
					</div>

					<div className="mt-8 flex gap-2">
						{SLIDES.map((s, i) => (
							<button
								key={s.title}
								type="button"
								onClick={() => setIndex(i)}
								aria-label={`${i + 1}. slayta git`}
								className={cn(
									"h-2 rounded-full bg-primary/20 transition-all",
									i === index ? "w-6 bg-primary" : "w-2",
								)}
							/>
						))}
					</div>
				</div>

				<div className="relative mx-auto aspect-square w-full max-w-[280px] md:max-w-sm">
					<div className="absolute inset-0 rounded-full bg-primary/10 blur-2xl" />
					<div className="absolute inset-6 rounded-full bg-primary/15" />
					<div className="absolute inset-0 grid place-items-center">
						{coverImages.map((product, i) => (
							<div
								key={product.id}
								className={cn(
									"absolute aspect-3/4 w-28 overflow-hidden rounded-xl shadow-pop ring-1 ring-white/40 sm:w-36",
									COVER_STYLES[i],
								)}
							>
								<Image
									src={product.imageUrl as string}
									alt={product.name}
									fill
									sizes="150px"
									className="object-cover"
									priority={i === 1}
								/>
							</div>
						))}
					</div>
				</div>
			</div>
		</section>
	);
}
