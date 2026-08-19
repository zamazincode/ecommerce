import { SectionHeading } from "@/components/ui/section-heading";
import { ScrollCarousel } from "@/components/ui/scroll-carousel";
import { ProductCard } from "@/components/product/product-card";
import type { ProductListDto } from "@/types";

/** Anasayfa/ürün detayı gibi yerlerde tekrar eden "başlık + yatay kaydırılan ürün listesi" bloğu. */
export function ProductCarousel({
	title,
	href,
	badge,
	products,
}: {
	title: string;
	href?: string;
	badge?: React.ReactNode;
	products: ProductListDto[];
}) {
	if (products.length === 0) return null;

	return (
		<section>
			<SectionHeading
				title={title}
				href={href}
				badge={badge}
				className="mb-4"
			/>
			<ScrollCarousel>
				{products.map((product) => (
					<div key={product.id} className="w-37.5 sm:w-45 lg:w-50">
						<ProductCard product={product} />
					</div>
				))}
			</ScrollCarousel>
		</section>
	);
}
