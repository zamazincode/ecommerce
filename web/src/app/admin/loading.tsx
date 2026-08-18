import { Skeleton } from "@/components/ui/skeleton";

export default function AdminLoading() {
	return (
		<div className="flex min-h-screen">
			<div className="w-56 shrink-0 border-r bg-muted/30 p-4">
				<Skeleton className="h-6 w-32" />
			</div>
			<main className="container-x flex-1 space-y-4 py-8">
				<Skeleton className="h-8 w-48" />
				<Skeleton className="h-32 w-full" />
				<Skeleton className="h-32 w-full" />
			</main>
		</div>
	);
}
