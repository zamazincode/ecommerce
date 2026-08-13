import { Skeleton } from "@/components/ui/skeleton";

export default function ProductLoading() {
	return (
		<main className="container-x grid gap-8 py-8 md:grid-cols-2">
			<Skeleton className="aspect-2/3 w-full rounded-lg" />
			<div className="space-y-3">
				<Skeleton className="h-8 w-3/4" />
				<Skeleton className="h-4 w-1/2" />
				<Skeleton className="h-6 w-1/3" />
			</div>
		</main>
	);
}
