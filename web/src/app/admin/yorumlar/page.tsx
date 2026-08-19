"use client";

import { useState } from "react";
import { CheckIcon, MessageSquareIcon, StarIcon, TrashIcon } from "lucide-react";
import {
	useAdminReviews,
	useApproveReview,
	useDeleteReview,
} from "@/hooks/use-admin-reviews";
import { PageHeader } from "@/components/admin/page-header";
import { RowAction, RowActions } from "@/components/admin/row-actions";
import { Badge } from "@/components/ui/badge";
import { Card } from "@/components/ui/card";
import { Tabs, TabsList, TabsTab } from "@/components/ui/tabs";
import { Pagination } from "@/components/ui/pagination";
import { TableSkeleton } from "@/components/ui/data-state";
import { EmptyState } from "@/components/ui/empty-state";
import {
	Dialog,
	DialogContent,
	DialogHeader,
	DialogTitle,
} from "@/components/ui/dialog";
import {
	Table,
	TableBody,
	TableCell,
	TableHead,
	TableHeader,
	TableRow,
} from "@/components/ui/table";
import type { components } from "@/types/api";

type AdminReviewDto = components["schemas"]["AdminReviewDto"];

export default function AdminReviewsPage() {
	const [onlyPending, setOnlyPending] = useState(true);
	const [page, setPage] = useState(1);
	const { data, isLoading } = useAdminReviews({ onlyPending, page });
	const approve = useApproveReview();
	const remove = useDeleteReview();
	const [detail, setDetail] = useState<AdminReviewDto | null>(null);

	return (
		<div className="space-y-6">
			<PageHeader
				title="Yorumlar"
				actions={
					<Tabs
						value={onlyPending ? "pending" : "all"}
						onValueChange={(value) => {
							setOnlyPending(value === "pending");
							setPage(1);
						}}
					>
						<TabsList>
							<TabsTab value="pending">Onay Bekleyen</TabsTab>
							<TabsTab value="all">Tümü</TabsTab>
						</TabsList>
					</Tabs>
				}
			/>

			<Card className="overflow-hidden p-0">
				{isLoading ? (
					<TableSkeleton rows={8} cols={6} />
				) : !data || data.items.length === 0 ? (
					<EmptyState
						icon={MessageSquareIcon}
						title={
							onlyPending
								? "Onay bekleyen yorum yok"
								: "Henüz hiç yorum yok"
						}
					/>
				) : (
					<Table>
						<TableHeader className="bg-muted/50">
							<TableRow className="hover:bg-transparent">
								<TableHead>Ürün</TableHead>
								<TableHead>Kullanıcı</TableHead>
								<TableHead>Puan</TableHead>
								<TableHead>Yorum</TableHead>
								<TableHead>Durum</TableHead>
								<TableHead className="text-right">
									İşlemler
								</TableHead>
							</TableRow>
						</TableHeader>
						<TableBody>
							{data.items.map((review) => {
								const rating = Number(review.rating);
								return (
									<TableRow key={review.id}>
										<TableCell>{review.productName}</TableCell>
										<TableCell className="text-sm text-muted-foreground">
											{review.userEmail ?? "—"}
										</TableCell>
										<TableCell>
											<div className="flex items-center gap-0.5">
												{Array.from({ length: 5 }).map((_, i) => (
													<StarIcon
														key={i}
														className={
															i < rating
																? "size-3.5 fill-warning text-warning"
																: "size-3.5 text-muted-foreground/30"
														}
													/>
												))}
												<span className="sr-only">{rating}/5</span>
											</div>
										</TableCell>
										<TableCell>
											<button
												type="button"
												className="block max-w-lg text-left"
												onClick={() => setDetail(review)}
											>
												<p className="line-clamp-2 text-sm whitespace-normal">
													{review.comment ?? "—"}
												</p>
											</button>
										</TableCell>
										<TableCell>
											<Badge
												variant={
													review.isApproved
														? "success"
														: "warning"
												}
											>
												{review.isApproved
													? "Yayında"
													: "Beklemede"}
											</Badge>
										</TableCell>
										<TableCell className="text-right">
											<RowActions>
												{!review.isApproved ? (
													<RowAction
														icon={CheckIcon}
														label="Onayla"
														onClick={() =>
															approve.mutate(
																review.id as number,
															)
														}
													/>
												) : null}
												<RowAction
													icon={TrashIcon}
													label="Sil"
													tone="danger"
													confirm={{
														title: "Bu yorum kalıcı olarak silinsin mi?",
														confirmLabel: "Sil",
													}}
													onClick={() =>
														remove.mutate(review.id as number)
													}
												/>
											</RowActions>
										</TableCell>
									</TableRow>
								);
							})}
						</TableBody>
					</Table>
				)}
			</Card>

			{data ? (
				<Pagination
					page={page}
					totalPages={Number(data.totalPages ?? 1)}
					hasPrevious={!!data.hasPrevious}
					hasNext={!!data.hasNext}
					onPageChange={setPage}
				/>
			) : null}

			<Dialog
				open={detail !== null}
				onOpenChange={(open) => !open && setDetail(null)}
			>
				<DialogContent>
					<DialogHeader>
						<DialogTitle>{detail?.productName}</DialogTitle>
					</DialogHeader>
					<p className="text-sm whitespace-pre-wrap">{detail?.comment}</p>
				</DialogContent>
			</Dialog>
		</div>
	);
}
