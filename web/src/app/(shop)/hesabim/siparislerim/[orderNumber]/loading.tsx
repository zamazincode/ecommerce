import { Skeleton } from "@/components/ui/skeleton";

export default function OrderDetailLoading() {
	return (
		<div className="space-y-6">
			<Skeleton className="h-7 w-56" />
			<Skeleton className="h-24 w-full rounded-lg" />
			<Skeleton className="h-48 w-full rounded-lg" />
		</div>
	);
}
