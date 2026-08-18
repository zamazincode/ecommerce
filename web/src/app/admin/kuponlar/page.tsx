"use client";

import { useAdminCoupons, useSetCouponActive } from "@/hooks/use-admin-coupons";
import { CouponFormDialog } from "@/components/admin/coupon-form-dialog";
import { COUPON_TYPE_LABELS } from "@/lib/enums";
import { Button } from "@/components/ui/button";
import { Switch } from "@/components/ui/switch";
import {
	Table,
	TableBody,
	TableCell,
	TableHead,
	TableHeader,
	TableRow,
} from "@/components/ui/table";
import { Skeleton } from "@/components/ui/skeleton";

export default function AdminCouponsPage() {
	const { data: coupons, isLoading } = useAdminCoupons();
	const setActive = useSetCouponActive();

	return (
		<div>
			<div className="mb-6 flex items-center justify-between">
				<h1 className="text-xl font-semibold">Kuponlar</h1>
				<CouponFormDialog trigger={<Button>Yeni Kupon</Button>} />
			</div>

			{isLoading ? (
				<Skeleton className="h-64 w-full" />
			) : (
				<Table>
					<TableHeader>
						<TableRow>
							<TableHead>Kod</TableHead>
							<TableHead>Tip</TableHead>
							<TableHead>Değer</TableHead>
							<TableHead>Kullanım</TableHead>
							<TableHead>Geçerlilik</TableHead>
							<TableHead>Aktif</TableHead>
						</TableRow>
					</TableHeader>
					<TableBody>
						{coupons?.map((coupon) => (
							<TableRow key={coupon.id}>
								<TableCell className="font-mono">
									{coupon.code}
								</TableCell>
								<TableCell>
									{
										COUPON_TYPE_LABELS[
											coupon.type as keyof typeof COUPON_TYPE_LABELS
										]
									}
								</TableCell>
								<TableCell>
									{coupon.value}
									{coupon.type === 0 ? "%" : " ₺"}
								</TableCell>
								<TableCell>
									{coupon.usedCount}
									{coupon.usageLimit
										? ` / ${coupon.usageLimit}`
										: ""}
								</TableCell>
								<TableCell className="text-xs text-muted-foreground">
									{new Date(coupon.validFrom).toLocaleDateString(
										"tr-TR",
									)}{" "}
									–{" "}
									{new Date(coupon.validTo).toLocaleDateString(
										"tr-TR",
									)}
								</TableCell>
								<TableCell>
									<Switch
										checked={coupon.isActive}
										onCheckedChange={(checked: boolean) =>
											setActive.mutate({
												id: coupon.id as number,
												isActive: checked,
											})
										}
									/>
								</TableCell>
							</TableRow>
						))}
					</TableBody>
				</Table>
			)}
		</div>
	);
}
