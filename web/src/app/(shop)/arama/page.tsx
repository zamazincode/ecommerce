import Link from "next/link";
import { search } from "@/lib/api/catalog";
import { ApiError } from "@/lib/api/client";
import { ProductCard } from "@/components/product/product-card";

type SearchParams = Promise<{ q?: string; page?: string }>;

const TOO_SHORT_MESSAGE = "Aramak için en az 2 karakter yazın.";

export default async function SearchPage({
	searchParams,
}: {
	searchParams: SearchParams;
}) {
	const { q, page } = await searchParams;

	if (!q || q.trim().length < 2) {
		return (
			<main className="container-x py-16 text-center text-muted-foreground">
				{TOO_SHORT_MESSAGE}
			</main>
		);
	}

	let result;
	try {
		result = await search(q, { page: page ? Number(page) : undefined });
	} catch (error) {
		// Girdi 2 karakter olsa bile normalizasyon sonrası (aksan/özel
		// karakter temizliği) kısalabiliyor — bu durumda backend 400 döner.
		// Bu, sistem hatası değil kullanıcı girdisi sorunu; error.tsx'e düşürme.
		if (error instanceof ApiError && error.status === 400) {
			return (
				<main className="container-x py-16 text-center text-muted-foreground">
					{TOO_SHORT_MESSAGE}
				</main>
			);
		}
		throw error;
	}

	return (
		<main className="container-x py-8">
			<h1 className="mb-4 text-lg font-semibold">
				&quot;{result.term}&quot; için {result.results.totalCount} sonuç
			</h1>

			{result.results.totalCount === 0 && result.didYouMean ? (
				<p className="mb-6 text-sm text-muted-foreground">
					Bunu mu demek istediniz:{" "}
					<Link
						href={`/arama?q=${encodeURIComponent(result.didYouMean)}`}
						className="font-medium text-foreground underline underline-offset-2 hover:no-underline"
					>
						{result.didYouMean}
					</Link>?
				</p>
			) : null}

			<div className="grid grid-cols-2 gap-4 sm:grid-cols-3 lg:grid-cols-4">
				{result.results.items.map((product) => (
					<ProductCard key={product.id} product={product} />
				))}
			</div>
		</main>
	);
}
