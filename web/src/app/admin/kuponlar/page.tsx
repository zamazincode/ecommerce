"use client";

import { CopyIcon, PlusIcon, TicketPercentIcon } from "lucide-react";
import { useAdminCoupons, useSetCouponActive } from "@/hooks/use-admin-coupons";
import { CouponFormDialog } from "@/components/admin/coupon-form-dialog";
import { COUPON_TYPE_LABELS } from "@/lib/enums";
import { formatPrice } from "@/lib/format";
import { cn } from "@/lib/utils";
import { PageHeader } from "@/components/admin/page-header";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Card } from "@/components/ui/card";
import { Switch } from "@/components/ui/switch";
import { TableSkeleton } from "@/components/ui/data-state";
import { EmptyState } from "@/components/ui/empty-state";
import { toast } from "@/components/ui/toast";
import {
	Table,
	TableBody,
	TableCell,
	TableHead,
	TableHeader,
	TableRow,
} from "@/components/ui/table";

export default function AdminCouponsPage() {
	const { data: coupons, isLoading } = useAdminCoupons();
	const setActive = useSetCouponActive();

	async function copyCode(code: string) {
		await navigator.clipboard.writeText(code);
		toast.add({ title: "Kopyalandı", type: "success" });
	}

	return (
		<div className="space-y-6">
			<PageHeader
				title="Kuponlar"
				actions={
					<CouponFormDialog
						trigger={
							<Button>
								<PlusIcon />
								Yeni Kupon
							</Button>
						}
					/>
				}
			/>

			<Card className="overflow-hidden p-0">
				{isLoading ? (
					<TableSkeleton rows={6} cols={6} />
				) : !coupons || coupons.length === 0 ? (
					<EmptyState
						icon={TicketPercentIcon}
						title="Henüz kupon yok"
						description="İlk kuponu oluşturarak başla."
					/>
				) : (
					<Table>
						<TableHeader className="bg-muted/50">
							<TableRow className="hover:bg-transparent">
								<TableHead>Kod</TableHead>
								<TableHead>Tip</TableHead>
								<TableHead>Değer</TableHead>
								<TableHead>Kullanım</TableHead>
								<TableHead>Geçerlilik</TableHead>
								<TableHead>Aktif</TableHead>
							</TableRow>
						</TableHeader>
						<TableBody>
							{coupons.map((coupon) => {
								const usedCount = Number(coupon.usedCount);
								const usageLimit =
									coupon.usageLimit != null
										? Number(coupon.usageLimit)
										: null;
								const isFull = usageLimit != null && usedCount >= usageLimit;
								const now = new Date();
								const validFrom = new Date(coupon.validFrom);
								const validTo = new Date(coupon.validTo);
								const isExpired = now > validTo;
								const isUpcoming = !isExpired && now < validFrom;

								return (
									<TableRow key={coupon.id}>
										<TableCell>
											<div className="flex items-center gap-1.5">
												<span className="font-mono text-sm">
													{coupon.code}
												</span>
												<button
													type="button"
													aria-label={`${coupon.code} kodunu kopyala`}
													className="text-muted-foreground hover:text-foreground"
													onClick={() => copyCode(coupon.code)}
												>
													<CopyIcon className="size-3.5" />
												</button>
											</div>
										</TableCell>
										<TableCell>
											{
												COUPON_TYPE_LABELS[
													coupon.type as keyof typeof COUPON_TYPE_LABELS
												]
											}
										</TableCell>
										<TableCell>
											{coupon.type === 0
												? `%${coupon.value}`
												: formatPrice(Number(coupon.value))}
										</TableCell>
										<TableCell>
											<p className="text-sm tabular-nums">
												{usedCount}
												{usageLimit != null ? `/${usageLimit}` : ""}
											</p>
											{usageLimit != null ? (
												<div className="mt-1 h-1 w-20 rounded-full bg-muted">
													<div
														className={cn(
															"h-1 rounded-full",
															isFull
																? "bg-destructive"
																: "bg-primary",
														)}
														style={{
															width: `${Math.min(100, (usedCount / usageLimit) * 100)}%`,
														}}
													/>
												</div>
											) : null}
										</TableCell>
										<TableCell>
											<p className="text-xs text-muted-foreground">
												{validFrom.toLocaleDateString("tr-TR")} –{" "}
												{validTo.toLocaleDateString("tr-TR")}
											</p>
											{isExpired ? (
												<Badge variant="destructive" className="mt-1">
													Süresi doldu
												</Badge>
											) : isUpcoming ? (
												<Badge variant="warning" className="mt-1">
													Yakında
												</Badge>
											) : null}
										</TableCell>
										<TableCell>
											<Switch
												checked={coupon.isActive}
												aria-label={`${coupon.code} kuponunu aktif/pasif yap`}
												onCheckedChange={(checked: boolean) =>
													setActive.mutate({
														id: coupon.id as number,
														isActive: checked,
													})
												}
											/>
										</TableCell>
									</TableRow>
								);
							})}
						</TableBody>
					</Table>
				)}
			</Card>
		</div>
	);
}
