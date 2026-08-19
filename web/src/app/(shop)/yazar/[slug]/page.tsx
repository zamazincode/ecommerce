import type { Metadata } from "next";
import { notFound } from "next/navigation";
import { BookOpenIcon } from "lucide-react";
import { getAuthorBySlug, getProductsByAuthor } from "@/lib/api/catalog";
import { ProductCard } from "@/components/product/product-card";
import { ProductListPagination } from "@/components/product/product-list-pagination";
import { ExpandableDescription } from "@/components/product/expandable-description";
import { Badge } from "@/components/ui/badge";
import { EmptyState } from "@/components/ui/empty-state";

export const revalidate = 3600;

type Params = Promise<{ slug: string }>;
type SearchParams = Promise<{ page?: string }>;

export async function generateMetadata({
	params,
}: {
	params: Params;
}): Promise<Metadata> {
	const { slug } = await params;
	const author = await getAuthorBySlug(slug);
	if (!author) return { title: "Yazar bulunamadı" };

	return {
		title: `${author.name} — Kitapları`,
		description: author.bio?.slice(0, 160),
	};
}

export default async function AuthorPage({
	params,
	searchParams,
}: {
	params: Params;
	searchParams: SearchParams;
}) {
	const { slug } = await params;
	const sp = await searchParams;
	const author = await getAuthorBySlug(slug);
	if (!author) notFound();

	const products = await getProductsByAuthor(author.id as number, {
		page: sp.page ? Number(sp.page) : undefined,
		pageSize: 24,
	});

	const page = Number(products.page ?? 1);
	const totalPages = Number(products.totalPages ?? 1);

	// Baş harflerden avatar — yazarların görseli API'de yok.
	const initials = author.name
		.split(" ")
		.filter(Boolean)
		.map((part) => part[0])
		.slice(0, 2)
		.join("")
		.toUpperCase();

	return (
		<main className="container-x py-8">
			<div className="flex flex-col items-start gap-5 rounded-2xl bg-primary-soft p-6 sm:flex-row sm:items-center md:p-8">
				<div className="grid size-20 shrink-0 place-items-center rounded-full bg-primary font-heading text-2xl text-primary-foreground">
					{initials}
				</div>
				<div>
					<h1 className="font-heading text-2xl font-semibold">
						{author.name}
					</h1>
					{author.bio ? (
						<div className="mt-2 max-w-2xl">
							<ExpandableDescription text={author.bio} lines={4} />
						</div>
					) : null}
					<Badge variant="brand-soft" className="mt-3">
						{products.totalCount} kitap
					</Badge>
				</div>
			</div>

			{products.items.length === 0 ? (
				<EmptyState
					icon={BookOpenIcon}
					title="Bu yazarın kayıtlı kitabı yok"
					className="mt-10"
				/>
			) : (
				<>
					<div className="mt-8 grid grid-cols-2 gap-4 sm:grid-cols-3 sm:gap-5 lg:grid-cols-5">
						{products.items.map((product) => (
							<ProductCard key={product.id} product={product} />
						))}
					</div>

					<ProductListPagination
						className="mt-8"
						basePath={`/yazar/${slug}`}
						params={{}}
						page={page}
						totalPages={totalPages}
						hasPrevious={Boolean(products.hasPrevious)}
						hasNext={Boolean(products.hasNext)}
					/>
				</>
			)}
		</main>
	);
}
