"use client";

import { useState } from "react";
import { useSearchParams, useRouter, usePathname } from "next/navigation";
import {
	useAdminOrders,
	useUpdateOrderStatus,
	ApiError,
} from "@/hooks/use-admin-orders";
import { ORDER_STATUS_LABELS, ORDER_STATUS_TRANSITIONS } from "@/lib/enums";
import { toast } from "@/components/ui/toast";
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

export default function AdminOrdersPage() {
	const router = useRouter();
	const pathname = usePathname();
	const searchParams = useSearchParams();
	const q = searchParams.get("q") ?? "";
	const page = Number(searchParams.get("page") ?? "1");

	const { data, isLoading } = useAdminOrders({ q: q || undefined, page });
	const updateStatus = useUpdateOrderStatus();

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
		router.push(`${pathname}?${params.toString()}`);
	}

	return (
		<div>
			<h1 className="mb-6 text-xl font-semibold">Siparişler</h1>

			<Input
				placeholder="Sipariş no veya e-posta ara…"
				value={searchValue}
				className="mb-4 max-w-sm"
				onChange={(e) => {
					setSearchValue(e.target.value);
					setParam("q", e.target.value || null);
				}}
			/>

			{isLoading ? null : (
				<Table>
					<TableHeader>
						<TableRow>
							<TableHead>Sipariş No</TableHead>
							<TableHead>Müşteri</TableHead>
							<TableHead>Tutar</TableHead>
							<TableHead>Tarih</TableHead>
							<TableHead>Durum</TableHead>
						</TableRow>
					</TableHeader>
					<TableBody>
						{data?.items.map((order) => {
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
										{order.total} ₺ ({order.itemCount} ürün)
									</TableCell>
									<TableCell>
										{new Date(order.createdAt).toLocaleDateString(
											"tr-TR",
										)}
									</TableCell>
									<TableCell>
										<select
											value={order.status}
											disabled={
												targets.length === 0 ||
												updateStatus.isPending
											}
											className="rounded-md border px-2 py-1 text-sm"
											onChange={(e) => {
												const status = Number(
													e.target.value,
												) as OrderStatus;
												updateStatus.mutate(
													{
														orderNumber: order.orderNumber,
														status,
													},
													{
														onError: (error) => {
															// Durum makinesi ihlali (400) YA DA yarış koşulu — arayı
															// istemci kopyası (ORDER_STATUS_TRANSITIONS) kapatmaya
															// çalışsa da sunucu her zaman son sözü söylüyor.
															const detail =
																error instanceof
																	ApiError &&
																error.body &&
																typeof error.body ===
																	"object" &&
																"detail" in error.body
																	? String(
																			error.body
																				.detail,
																		)
																	: "Durum değiştirilemedi.";
															toast.add({
																title: detail,
																type: "error",
															});
														},
													},
												);
											}}
										>
											<option value={order.status}>
												{
													ORDER_STATUS_LABELS[
														order.status as OrderStatus
													]
												}
											</option>
											{targets.map((target) => (
												<option key={target} value={target}>
													{ORDER_STATUS_LABELS[target]}
												</option>
											))}
										</select>
									</TableCell>
								</TableRow>
							);
						})}
					</TableBody>
				</Table>
			)}
		</div>
	);
}
