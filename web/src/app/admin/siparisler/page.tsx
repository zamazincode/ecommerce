"use client";

import { useState } from "react";
import { useSearchParams, useRouter, usePathname } from "next/navigation";
import { SearchIcon, ShoppingCartIcon } from "lucide-react";
import {
	useAdminOrders,
	useUpdateOrderStatus,
	ApiError,
} from "@/hooks/use-admin-orders";
import { ORDER_STATUS_LABELS, ORDER_STATUS_TRANSITIONS, ORDER_STATUS_TONES } from "@/lib/enums";
import { toast } from "@/components/ui/toast";
import { PageHeader } from "@/components/admin/page-header";
import { Badge } from "@/components/ui/badge";
import { Card } from "@/components/ui/card";
import { NativeSelect } from "@/components/ui/native-select";
import { Price } from "@/components/ui/price";
import { Pagination } from "@/components/ui/pagination";
import { TableSkeleton } from "@/components/ui/data-state";
import { EmptyState } from "@/components/ui/empty-state";
import {
	AlertDialog,
	AlertDialogAction,
	AlertDialogCancel,
	AlertDialogContent,
	AlertDialogDescription,
	AlertDialogFooter,
	AlertDialogHeader,
	AlertDialogTitle,
} from "@/components/ui/alert-dialog";
import {
	Table,
	TableBody,
	TableCell,
	TableHead,
	TableHeader,
	TableRow,
} from "@/components/ui/table";
import { Input } from "@/components/ui/input";
import type { components } from "@/types/api";

type OrderStatus = components["schemas"]["OrderStatus"];

// Geri dönüşsüz geçişler — seçilince doğrudan uygulanmıyor, önce onay isteniyor.
const IRREVERSIBLE_TARGETS = new Set<OrderStatus>([5, 6]);

export default function AdminOrdersPage() {
	const router = useRouter();
	const pathname = usePathname();
	const searchParams = useSearchParams();
	const q = searchParams.get("q") ?? "";
	const page = Number(searchParams.get("page") ?? "1");
	const statusParam = searchParams.get("status");
	const status =
		statusParam !== null ? (Number(statusParam) as OrderStatus) : undefined;

	const { data, isLoading } = useAdminOrders({ q: q || undefined, page, status });
	const updateStatus = useUpdateOrderStatus();

	const [pendingChange, setPendingChange] = useState<{
		orderNumber: string;
		status: OrderStatus;
	} | null>(null);

	// KONTROLLÜ input — bkz. admin/urunler/page.tsx'teki aynı düzeltme:
	// `defaultValue={q}` Base UI'ın uncontrolled/controlled uyarısını
	// tetikliyordu çünkü `q` her tuş vuruşunda URL üzerinden değişiyor.
	// Senkron render sırasında yapılıyor, `useEffect` içinde değil.
	const [searchValue, setSearchValue] = useState(q);
	const [prevQ, setPrevQ] = useState(q);
	if (q !== prevQ) {
		setPrevQ(q);
		setSearchValue(q);
	}

	function setParam(key: string, value: string | null) {
		const params = new URLSearchParams(searchParams.toString());
		if (value) params.set(key, value);
		else params.delete(key);
		if (key !== "page") params.delete("page");
		router.push(`${pathname}?${params.toString()}`);
	}

	function applyStatusChange(orderNumber: string, target: OrderStatus) {
		updateStatus.mutate(
			{ orderNumber, status: target },
			{
				onError: (error) => {
					// Durum makinesi ihlali (400) YA DA yarış koşulu — arayı
					// istemci kopyası (ORDER_STATUS_TRANSITIONS) kapatmaya
					// çalışsa da sunucu her zaman son sözü söylüyor.
					const detail =
						error instanceof ApiError &&
						error.body &&
						typeof error.body === "object" &&
						"detail" in error.body
							? String(error.body.detail)
							: "Durum değiştirilemedi.";
					toast.add({ title: detail, type: "error" });
				},
			},
		);
	}

	function handleStatusSelect(orderNumber: string, target: OrderStatus) {
		if (IRREVERSIBLE_TARGETS.has(target)) {
			setPendingChange({ orderNumber, status: target });
			return;
		}
		applyStatusChange(orderNumber, target);
	}

	return (
		<div className="space-y-6">
			<PageHeader title="Siparişler" description={data ? `${data.totalCount} sipariş` : undefined} />

			<Card size="sm" className="flex-row flex-wrap items-center gap-3">
				<div className="relative max-w-sm flex-1">
					<SearchIcon className="pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2 text-muted-foreground" />
					<Input
						placeholder="Sipariş no veya e-posta ara…"
						value={searchValue}
						className="pl-9"
						onChange={(e) => {
							setSearchValue(e.target.value);
							setParam("q", e.target.value || null);
						}}
					/>
				</div>
				<NativeSelect
					className="w-auto"
					value={statusParam ?? ""}
					onChange={(e) => setParam("status", e.target.value || null)}
				>
					<option value="">Tüm durumlar</option>
					{Object.entries(ORDER_STATUS_LABELS).map(([value, label]) => (
						<option key={value} value={value}>
							{label}
						</option>
					))}
				</NativeSelect>
			</Card>

			<Card className="overflow-hidden p-0">
				{isLoading ? (
					<TableSkeleton rows={10} cols={5} />
				) : !data || data.items.length === 0 ? (
					<EmptyState
						icon={ShoppingCartIcon}
						title="Sipariş bulunamadı"
						description="Arama kriterlerine uyan sipariş yok."
					/>
				) : (
					<Table>
						<TableHeader className="bg-muted/50">
							<TableRow className="hover:bg-transparent">
								<TableHead>Sipariş No</TableHead>
								<TableHead>Müşteri</TableHead>
								<TableHead>Tutar</TableHead>
								<TableHead>Tarih</TableHead>
								<TableHead>Durum</TableHead>
							</TableRow>
						</TableHeader>
						<TableBody>
							{data.items.map((order) => {
								const targets =
									ORDER_STATUS_TRANSITIONS[order.status as OrderStatus];

								return (
									<TableRow key={order.id as number}>
										<TableCell className="font-mono text-sm">
											{order.orderNumber}
										</TableCell>
										<TableCell>
											{order.customerName ?? "—"}
											<div className="text-xs text-muted-foreground">
												{order.customerEmail}
											</div>
										</TableCell>
										<TableCell>
											<Price size="sm" price={Number(order.total)} />
											<div className="text-xs text-muted-foreground">
												{order.itemCount} ürün
											</div>
										</TableCell>
										<TableCell>
											{new Date(order.createdAt).toLocaleDateString(
												"tr-TR",
											)}
										</TableCell>
										<TableCell>
											<div className="flex items-center gap-2">
												<Badge
													variant={
														ORDER_STATUS_TONES[
															order.status as OrderStatus
														]
													}
												>
													{
														ORDER_STATUS_LABELS[
															order.status as OrderStatus
														]
													}
												</Badge>
												{targets.length > 0 ? (
													<NativeSelect
														className="h-8 w-40"
														value=""
														disabled={updateStatus.isPending}
														onChange={(e) => {
															if (!e.target.value) return;
															const target = Number(
																e.target.value,
															) as OrderStatus;
															handleStatusSelect(
																order.orderNumber,
																target,
															);
															e.target.value = "";
														}}
													>
														<option value="" disabled>
															Durumu Değiştir
														</option>
														{targets.map((target) => (
															<option
																key={target}
																value={target}
															>
																{ORDER_STATUS_LABELS[target]}
															</option>
														))}
													</NativeSelect>
												) : null}
											</div>
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
					onPageChange={(target) => setParam("page", String(target))}
				/>
			) : null}

			<AlertDialog
				open={pendingChange !== null}
				onOpenChange={(open) => !open && setPendingChange(null)}
			>
				<AlertDialogContent>
					<AlertDialogHeader>
						<AlertDialogTitle>
							{pendingChange
								? `Sipariş ${ORDER_STATUS_LABELS[pendingChange.status]} olarak işaretlensin mi?`
								: ""}
						</AlertDialogTitle>
						<AlertDialogDescription>
							Bu işlem geri alınamaz. Stok iade edilecek.
						</AlertDialogDescription>
					</AlertDialogHeader>
					<AlertDialogFooter>
						<AlertDialogCancel>Vazgeç</AlertDialogCancel>
						<AlertDialogAction
							variant="destructive-solid"
							onClick={() => {
								if (pendingChange) {
									applyStatusChange(
										pendingChange.orderNumber,
										pendingChange.status,
									);
								}
								setPendingChange(null);
							}}
						>
							Onayla
						</AlertDialogAction>
					</AlertDialogFooter>
				</AlertDialogContent>
			</AlertDialog>
		</div>
	);
}
