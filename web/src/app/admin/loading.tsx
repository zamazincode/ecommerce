import { Skeleton } from "@/components/ui/skeleton";
import { TableSkeleton } from "@/components/ui/data-state";

export default function AdminLoading() {
	return (
		<div className="flex min-h-screen bg-surface">
			<div className="hidden w-60 shrink-0 flex-col border-r bg-background p-3 lg:flex">
				<Skeleton className="mb-4 h-8 w-32" />
				{Array.from({ length: 7 }).map((_, i) => (
					<Skeleton key={i} className="mb-1 h-10 w-full rounded-lg" />
				))}
			</div>
			<div className="flex min-w-0 flex-1 flex-col">
				<div className="h-14 border-b" />
				<main className="flex-1 px-4 py-6 sm:px-6 lg:px-8">
					<div className="mx-auto w-full max-w-[1400px] space-y-6">
						<Skeleton className="h-8 w-48" />
						<TableSkeleton rows={8} cols={5} />
					</div>
				</main>
			</div>
		</div>
	);
}
