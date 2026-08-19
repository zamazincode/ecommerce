import { getHome, getCategoryTree } from "@/lib/api/catalog";
import { HeroSlider } from "@/components/home/hero-slider";
import { PromoBanners } from "@/components/home/promo-banners";
// import { CollectionTiles } from "@/components/home/collection-tiles";
import { CategoryStrip } from "@/components/layout/category-strip";
import { ProductCarousel } from "@/components/product/product-carousel";
// import { SectionHeading } from "@/components/ui/section-heading";
import { Badge } from "@/components/ui/badge";

export const revalidate = 60;

export default async function HomePage() {
	const [home, tree] = await Promise.all([getHome(), getCategoryTree()]);

	return (
		<div className="space-y-12 py-8 md:space-y-16">
			<HeroSlider covers={home.bestsellers} />

			<CategoryStrip categories={tree} />

			<div className="container-x">
				<PromoBanners />
			</div>

			{home.bestsellers.length > 0 ? (
				<section className="section-band py-12">
					<div className="container-x">
						<ProductCarousel
							title="Çok Satan Kitaplar"
							href="/kategori/kitap"
							products={home.bestsellers}
						/>
					</div>
				</section>
			) : null}

			{home.discounted.length > 0 ? (
				<div className="container-x">
					<ProductCarousel
						title="İndirimdekiler"
						products={home.discounted}
						badge={
							<Badge variant="accent">
								%&apos;ye varan indirim
							</Badge>
						}
					/>
				</div>
			) : null}

			{/*
			<section className="section-band py-12">
				<div className="container-x">
					<SectionHeading
						title="Senin İçin Seçtiklerimiz"
						className="mb-4"
					/>
					<CollectionTiles categories={tree} />
				</div>
			</section>
			*/}

			{home.newArrivals.length > 0 ? (
				<div className="container-x">
					<ProductCarousel
						title="Yeni Gelenler"
						products={home.newArrivals}
					/>
				</div>
			) : null}
		</div>
	);
}
