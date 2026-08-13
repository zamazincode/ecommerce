import type { Metadata } from "next";
import Image from "next/image";
import { notFound } from "next/navigation";
import { getProductBySlug } from "@/lib/api/catalog";
import { formatPrice } from "@/lib/format";
import { toSafeJsonLd } from "@/lib/utils";

export const revalidate = 60;

type Params = Promise<{ slug: string }>;

export async function generateStaticParams() {
	// Build sırasında en popüler ürünleri önceden üret. Kalan binlerce ürün
	// ilk ziyarette üretilir, sonra 60sn'lik ISR'a girer — 1959 ürünün
	// hepsini build'de üretmek gereksiz yere build süresini uzatır.
	const response = await fetch(`${process.env.API_INTERNAL_URL}/api/home`);
	if (!response.ok) return [];

	const home = await response.json();
	const slugs = new Set<string>([
		...home.bestsellers.map((p: { slug: string }) => p.slug),
		...home.newArrivals.map((p: { slug: string }) => p.slug),
	]);

	return Array.from(slugs).map((slug) => ({ slug }));
}

export async function generateMetadata({
	params,
}: {
	params: Params;
}): Promise<Metadata> {
	const { slug } = await params;
	const product = await getProductBySlug(slug);
	if (!product) return { title: "Ürün bulunamadı" };

	return {
		title: `${product.name}${product.authors.length ? ` — ${product.authors[0].name}` : ""}`,
		description: product.description?.slice(0, 160),
		openGraph: {
			images: product.imageUrls[0] ? [product.imageUrls[0]] : [],
		},
	};
}

export default async function ProductPage({ params }: { params: Params }) {
	const { slug } = await params;
	const product = await getProductBySlug(slug);

	if (!product) notFound();

	// Google shop listte çıkması için
	const jsonLd = {
		"@context": "https://schema.org",
		"@type": "Product",
		name: product.name,
		image: product.imageUrls,
		description: product.description,
		offers: {
			"@type": "Offer",
			priceCurrency: "TRY",
			price: product.effectivePrice,
			availability: product.inStock
				? "https://schema.org/InStock"
				: "https://schema.org/OutOfStock",
		},
	};

	return (
		<main className="container-x grid gap-8 py-8 md:grid-cols-2">
			{/* eslint-disable-next-line @next/next/no-script-component-in-head -- JSON-LD, çalıştırılabilir kod değil */}
			<script
				type="application/ld+json"
				dangerouslySetInnerHTML={{ __html: toSafeJsonLd(jsonLd) }}
			/>

			<div className="relative aspect-2/3 w-full overflow-hidden rounded-lg bg-muted">
				{product.imageUrls[0] ? (
					<Image
						src={product.imageUrls[0]}
						alt={product.name}
						fill
						priority
						sizes="(max-width: 768px) 100vw, 50vw"
						className="object-cover"
					/>
				) : null}
			</div>

			<div>
				<h1 className="text-2xl font-semibold">{product.name}</h1>
				{product.authors.length > 0 ? (
					<p className="text-muted-foreground">
						{product.authors.map((a) => a.name).join(", ")}
					</p>
				) : null}

				<p className="mt-4 text-2xl font-bold">
					{formatPrice(product.effectivePrice as number)}
				</p>
				{product.discountedPrice != null ? (
					<p className="text-muted-foreground line-through">
						{formatPrice(product.price as number)}
					</p>
				) : null}

				<p className="mt-4 text-sm">
					{product.inStock ? "Stokta var" : "Stokta yok"}
				</p>

				{product.description ? (
					<p className="mt-6 whitespace-pre-line text-sm text-muted-foreground">
						{product.description}
					</p>
				) : null}

				{/* Sepete Ekle butonu */}
			</div>
		</main>
	);
}
