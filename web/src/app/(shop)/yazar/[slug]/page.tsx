import { notFound } from "next/navigation";
import { getAuthorBySlug, getProductsByAuthor } from "@/lib/api/catalog";
import { ProductCard } from "@/components/product/product-card";

export const revalidate = 3600;

type Params = Promise<{ slug: string }>;

export default async function AuthorPage({ params }: { params: Params }) {
	const { slug } = await params;
	const author = await getAuthorBySlug(slug);
	if (!author) notFound();

	const products = await getProductsByAuthor(author.id as number, {
		pageSize: 24,
	});

	return (
		<main className="container-x py-8">
			<h1 className="text-2xl font-semibold">{author.name}</h1>
			{author.bio ? (
				<p className="mt-2 max-w-2xl text-muted-foreground">
					{author.bio}
				</p>
			) : null}

			<div className="mt-8 grid grid-cols-2 gap-4 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-6">
				{products.items.map((product) => (
					<ProductCard key={product.id} product={product} />
				))}
			</div>
			{products.items.length === 0 ? (
				<p className="mt-8 text-muted-foreground">
					Bu yazarın kayıtlı kitabı yok.
				</p>
			) : null}
		</main>
	);
}
