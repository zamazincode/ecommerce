import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";
import { CheckIcon, RotateCcwIcon, ShieldCheckIcon, TruckIcon, XIcon } from "lucide-react";
import {
	getProductBySlug,
	getRelatedProducts,
	getCategoryTree,
} from "@/lib/api/catalog";
import { findCategoryPath } from "@/lib/category-path";
import { toSafeJsonLd } from "@/lib/utils";
import { BOOK_BINDING_LABELS } from "@/lib/enums";
import { FREE_SHIPPING_THRESHOLD } from "@/lib/shipping";
import { AddToCartButton } from "@/components/product/add-to-cart-button";
import { FavoriteButton } from "@/components/product/favorite-button";
import { ProductGallery } from "@/components/product/product-gallery";
import { ProductCarousel } from "@/components/product/product-carousel";
import { ExpandableDescription } from "@/components/product/expandable-description";
import { Breadcrumb } from "@/components/common/breadcrumb";
import { Price } from "@/components/ui/price";
import {
	Accordion,
	AccordionItem,
	AccordionTrigger,
	AccordionContent,
} from "@/components/ui/accordion";
import { Separator } from "@/components/ui/separator";

export const revalidate = 60;

type Params = Promise<{ slug: string }>;

export async function generateStaticParams() {
	// Build sırasında en popüler ürünleri önceden üret. Kalan binlerce ürün
	// ilk ziyarette üretilir, sonra 60sn'lik ISR'a girer — 1959 ürünün
	// hepsini build'de üretmek gereksiz yere build süresini uzatır.
	try {
		const response = await fetch(
			`${process.env.API_INTERNAL_URL}/api/home`,
		);
		if (!response.ok) return [];

		const home = await response.json();
		const slugs = new Set<string>([
			...home.bestsellers.map((p: { slug: string }) => p.slug),
			...home.newArrivals.map((p: { slug: string }) => p.slug),
		]);

		return Array.from(slugs).map((slug) => ({ slug }));
	} catch {
		return [];
	}
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

	const [related, tree] = await Promise.all([
		getRelatedProducts(product.id as number),
		getCategoryTree(),
	]);

	const categoryPath = findCategoryPath(tree, product.category.slug) ?? [];
	const price = product.price as number;
	const discountedPrice = product.discountedPrice as number | null;
	const stock = product.stock as number;

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
		<main className="container-x space-y-10 py-6">
			{/* eslint-disable-next-line @next/next/no-script-component-in-head -- JSON-LD, çalıştırılabilir kod değil */}
			<script
				type="application/ld+json"
				dangerouslySetInnerHTML={{ __html: toSafeJsonLd(jsonLd) }}
			/>

			<Breadcrumb
				items={[
					...categoryPath.map((c) => ({
						label: c.name,
						href: `/kategori/${c.slug}`,
					})),
					{ label: product.name },
				]}
			/>

			<div className="grid gap-8 lg:grid-cols-[minmax(0,420px)_1fr_320px]">
				<ProductGallery images={product.imageUrls} productName={product.name} />

				<div>
					<Link
						href={`/kategori/${product.category.slug}`}
						className="text-xs font-medium tracking-wide text-muted-foreground uppercase hover:text-primary"
					>
						{product.category.name}
					</Link>
					<h1 className="mt-1 font-heading text-2xl font-semibold md:text-3xl">
						{product.name}
					</h1>
					{product.authors.length > 0 ? (
						<p className="mt-2 text-muted-foreground">
							{product.authors.map((author, i) => (
								<span key={author.id}>
									{i > 0 ? ", " : ""}
									<Link
										href={`/yazar/${author.slug}`}
										className="text-primary hover:underline"
									>
										{author.name}
									</Link>
								</span>
							))}
						</p>
					) : null}
					{product.publisher || product.brand ? (
						<p className="mt-1 text-sm text-muted-foreground">
							{product.publisher ? (
								<>
									Yayınevi:{" "}
									<span className="text-foreground">
										{product.publisher.name}
									</span>
								</>
							) : null}
							{product.publisher && product.brand ? " · " : null}
							{product.brand ? (
								<>
									Marka:{" "}
									<span className="text-foreground">
										{product.brand.name}
									</span>
								</>
							) : null}
						</p>
					) : null}

					<Separator className="my-6" />

					<Accordion defaultValue={["info"]}>
						<AccordionItem value="info">
							<AccordionTrigger>Ürün Bilgileri</AccordionTrigger>
							<AccordionContent className="space-y-4">
								{product.bookDetail ? (
									<dl className="grid grid-cols-2 gap-x-4 gap-y-2 text-sm">
										{product.bookDetail.isbn ? (
											<>
												<dt className="text-muted-foreground">
													ISBN
												</dt>
												<dd>{product.bookDetail.isbn}</dd>
											</>
										) : null}
										{product.bookDetail.pageCount ? (
											<>
												<dt className="text-muted-foreground">
													Sayfa Sayısı
												</dt>
												<dd>{product.bookDetail.pageCount}</dd>
											</>
										) : null}
										{product.bookDetail.language ? (
											<>
												<dt className="text-muted-foreground">
													Dil
												</dt>
												<dd>{product.bookDetail.language}</dd>
											</>
										) : null}
										{product.bookDetail.publishedYear ? (
											<>
												<dt className="text-muted-foreground">
													Basım Yılı
												</dt>
												<dd>{product.bookDetail.publishedYear}</dd>
											</>
										) : null}
										<dt className="text-muted-foreground">
											Cilt Tipi
										</dt>
										<dd>
											{BOOK_BINDING_LABELS[product.bookDetail.binding]}
										</dd>
									</dl>
								) : null}

								{product.description ? (
									<ExpandableDescription text={product.description} />
								) : null}
							</AccordionContent>
						</AccordionItem>
					</Accordion>
				</div>

				<div className="space-y-4 rounded-2xl border p-5 shadow-card lg:sticky lg:top-24">
					<Price
						price={price}
						discountedPrice={discountedPrice}
						size="lg"
						showBadge
					/>

					<p
						className={
							product.inStock
								? "flex items-center gap-1.5 text-sm text-success"
								: "flex items-center gap-1.5 text-sm text-destructive"
						}
					>
						{product.inStock ? (
							<CheckIcon className="size-4" />
						) : (
							<XIcon className="size-4" />
						)}
						{product.inStock ? "Stokta var, hemen kargoda" : "Stokta yok"}
					</p>

					<AddToCartButton
						productId={product.id as number}
						inStock={product.inStock}
						stock={stock}
					/>

					<FavoriteButton
						productId={product.id as number}
						variant="expanded"
					/>

					<ul className="space-y-2 border-t pt-4 text-xs text-muted-foreground">
						<li className="flex items-center gap-2">
							<TruckIcon className="size-4" />
							{FREE_SHIPPING_THRESHOLD} ₺ üzeri kargo bedava
						</li>
						<li className="flex items-center gap-2">
							<RotateCcwIcon className="size-4" />
							14 gün içinde iade
						</li>
						<li className="flex items-center gap-2">
							<ShieldCheckIcon className="size-4" />
							Güvenli ödeme
						</li>
					</ul>
				</div>
			</div>

			<ProductCarousel title="Benzer Ürünler" products={related} />
		</main>
	);
}
