import { Skeleton } from "@/components/ui/skeleton";

export default function ShopLoading() {
	return (
		<main className="container-x py-8">
			<div className="grid grid-cols-2 gap-4 sm:grid-cols-3 lg:grid-cols-4">
				{Array.from({ length: 8 }).map((_, i) => (
					<Skeleton
						key={i}
						className="aspect-2/3 w-full rounded-md"
					/>
				))}
			</div>
		</main>
	);
}
