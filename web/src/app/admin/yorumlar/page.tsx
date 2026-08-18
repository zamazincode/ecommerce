"use client";

import { useState } from "react";
import {
	useAdminReviews,
	useApproveReview,
	useDeleteReview,
} from "@/hooks/use-admin-reviews";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import {
	Table,
	TableBody,
	TableCell,
	TableHead,
	TableHeader,
	TableRow,
} from "@/components/ui/table";
import { Skeleton } from "@/components/ui/skeleton";

export default function AdminReviewsPage() {
	const [onlyPending, setOnlyPending] = useState(true);
	const { data, isLoading } = useAdminReviews(onlyPending);
	const approve = useApproveReview();
	const remove = useDeleteReview();

	return (
		<div>
			<div className="mb-6 flex items-center justify-between">
				<h1 className="text-xl font-semibold">Yorumlar</h1>
				<label className="flex items-center gap-2 text-sm">
					<input
						type="checkbox"
						checked={onlyPending}
						onChange={(e) => setOnlyPending(e.target.checked)}
					/>
					Yalnızca onay bekleyenler
				</label>
			</div>

			{isLoading ? (
				<Skeleton className="h-64 w-full" />
			) : data?.items.length === 0 ? (
				<p className="text-sm text-muted-foreground">
					{onlyPending
						? "Onay bekleyen yorum yok."
						: "Henüz hiç yorum yok."}
				</p>
			) : (
				<Table>
					<TableHeader>
						<TableRow>
							<TableHead>Ürün</TableHead>
							<TableHead>Kullanıcı</TableHead>
							<TableHead>Puan</TableHead>
							<TableHead>Yorum</TableHead>
							<TableHead>Durum</TableHead>
							<TableHead className="text-right">İşlemler</TableHead>
						</TableRow>
					</TableHeader>
					<TableBody>
						{data?.items.map((review) => (
							<TableRow key={review.id}>
								<TableCell>{review.productName}</TableCell>
								<TableCell className="text-sm text-muted-foreground">
									{review.userEmail ?? "—"}
								</TableCell>
								<TableCell>{review.rating}/5</TableCell>
								<TableCell className="max-w-xs truncate">
									{review.comment ?? "—"}
								</TableCell>
								<TableCell>
									<Badge
										variant={
											review.isApproved ? "default" : "secondary"
										}
									>
										{review.isApproved ? "Yayında" : "Beklemede"}
									</Badge>
								</TableCell>
								<TableCell className="text-right space-x-2">
									{!review.isApproved ? (
										<Button
											variant="ghost"
											size="sm"
											onClick={() =>
												approve.mutate(review.id as number)
											}
										>
											Onayla
										</Button>
									) : null}
									<Button
										variant="ghost"
										size="sm"
										onClick={() => {
											if (
												confirm(
													"Bu yorum kalıcı olarak silinsin mi?",
												)
											)
												remove.mutate(review.id as number);
										}}
									>
										Sil
									</Button>
								</TableCell>
							</TableRow>
						))}
					</TableBody>
				</Table>
			)}
		</div>
	);
}
