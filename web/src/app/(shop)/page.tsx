import { getHome } from "@/lib/api/catalog";
import { ProductCard } from "@/components/product/product-card";

export const revalidate = 60;

export default async function HomePage() {
	const home = await getHome();

	return (
		<main className="container-x space-y-10 py-8">
			<section>
				<h2 className="mb-4 text-lg font-semibold">Çok Satanlar</h2>
				<div className="grid grid-cols-2 gap-4 sm:grid-cols-3 lg:grid-cols-5">
					{home.bestsellers.map((product) => (
						<ProductCard key={product.id} product={product} />
					))}
				</div>
			</section>

			<section>
				<h2 className="mb-4 text-lg font-semibold">Yeni Gelenler</h2>
				<div className="grid grid-cols-2 gap-4 sm:grid-cols-3 lg:grid-cols-5">
					{home.newArrivals.map((product) => (
						<ProductCard key={product.id} product={product} />
					))}
				</div>
			</section>

			<section>
				<h2 className="mb-4 text-lg font-semibold">İndirimdekiler</h2>
				<div className="grid grid-cols-2 gap-4 sm:grid-cols-3 lg:grid-cols-5">
					{home.discounted.map((product) => (
						<ProductCard key={product.id} product={product} />
					))}
				</div>
			</section>
		</main>
	);
}
