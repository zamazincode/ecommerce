import { Skeleton } from "@/components/ui/skeleton";

export default function OrderHistoryLoading() {
	return (
		<div className="space-y-3">
			<Skeleton className="h-7 w-40" />
			{Array.from({ length: 4 }).map((_, i) => (
				<Skeleton key={i} className="h-16 w-full rounded-lg" />
			))}
		</div>
	);
}
