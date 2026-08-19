"use client";

import { Fragment, useState } from "react";
import { EyeIcon, HistoryIcon } from "lucide-react";
import { useAdminAuditLogs } from "@/hooks/use-admin-audit-logs";
import { AUDIT_ACTION_LABELS, AUDIT_ACTION_TONES } from "@/lib/enums";
import { PageHeader } from "@/components/admin/page-header";
import { RowAction, RowActions } from "@/components/admin/row-actions";
import { Badge } from "@/components/ui/badge";
import { Card } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { NativeSelect } from "@/components/ui/native-select";
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

type AuditLogDto = components["schemas"]["AuditLogDto"];

// AuditedEntities.Types (Persistence/Auditing/AuditedEntities.cs) ile
// BİREBİR aynı liste — backend bu tiplerin dışında hiçbir şey yazmıyor,
// filtrede başka bir seçenek sunmanın anlamı yok.
const ENTITY_TYPES = [
	"Product",
	"Category",
	"Coupon",
	"Review",
	"ProductImage",
	"Order",
];
const ACTIONS = ["Created", "Updated", "Deleted"];

// OldValues/NewValues STRING olarak geliyor (jsonb kolonu) — istemci
// JSON.parse eder, object'e çevirmek gereksiz iki kez ayrıştırma olurdu.
// Bozuk/eksik JSON'a karşı `try/catch` — Created'da OldValues, Deleted'da
// NewValues zaten `null` geliyor, bu normal.
function parseAuditValues(oldValues: string | null, newValues: string | null) {
	let oldParsed: Record<string, unknown> | null = null;
	let newParsed: Record<string, unknown> | null = null;
	try {
		oldParsed = oldValues ? JSON.parse(oldValues) : null;
	} catch {
		oldParsed = null;
	}
	try {
		newParsed = newValues ? JSON.parse(newValues) : null;
	} catch {
		newParsed = null;
	}
	const fields = Array.from(
		new Set([
			...Object.keys(oldParsed ?? {}),
			...Object.keys(newParsed ?? {}),
		]),
	);
	return { oldParsed, newParsed, fields };
}

function formatFieldValue(value: unknown): string {
	if (value === null || value === undefined) return "—";
	if (typeof value === "object") return JSON.stringify(value);
	return String(value);
}

export default function AdminAuditLogPage() {
	const [entityType, setEntityType] = useState("");
	const [action, setAction] = useState("");
	const [page, setPage] = useState(1);
	const { data, isLoading } = useAdminAuditLogs({ entityType, action, page });
	const [detail, setDetail] = useState<AuditLogDto | null>(null);

	const detailValues = detail
		? parseAuditValues(detail.oldValues, detail.newValues)
		: null;

	return (
		<div className="space-y-6">
			<PageHeader
				title="Denetim Kaydı"
				description="Admin işlemlerinin kalıcı izi"
			/>

			<Card size="sm" className="flex-row flex-wrap items-center gap-3">
				<NativeSelect
					className="w-auto"
					value={entityType}
					onChange={(e) => {
						setEntityType(e.target.value);
						setPage(1);
					}}
				>
					<option value="">Tüm varlıklar</option>
					{ENTITY_TYPES.map((t) => (
						<option key={t} value={t}>
							{t}
						</option>
					))}
				</NativeSelect>
				<NativeSelect
					className="w-auto"
					value={action}
					onChange={(e) => {
						setAction(e.target.value);
						setPage(1);
					}}
				>
					<option value="">Tüm işlemler</option>
					{ACTIONS.map((a) => (
						<option key={a} value={a}>
							{AUDIT_ACTION_LABELS[a] ?? a}
						</option>
					))}
				</NativeSelect>
				{entityType || action ? (
					<Button
						variant="ghost"
						size="sm"
						onClick={() => {
							setEntityType("");
							setAction("");
							setPage(1);
						}}
					>
						Temizle
					</Button>
				) : null}
			</Card>

			<Card className="overflow-hidden p-0">
				{isLoading ? (
					<TableSkeleton rows={10} cols={6} />
				) : !data || data.items.length === 0 ? (
					<EmptyState
						icon={HistoryIcon}
						title="Kayıt bulunamadı"
						description="Seçili filtrelere uyan denetim kaydı yok."
					/>
				) : (
					<Table>
						<TableHeader className="bg-muted/50">
							<TableRow className="hover:bg-transparent">
								<TableHead>Tarih</TableHead>
								<TableHead>Kullanıcı</TableHead>
								<TableHead>Varlık</TableHead>
								<TableHead>İşlem</TableHead>
								<TableHead>Değişiklik</TableHead>
								<TableHead className="text-right">
									İşlemler
								</TableHead>
							</TableRow>
						</TableHeader>
						<TableBody>
							{data.items.map((log) => {
								const { fields } = parseAuditValues(
									log.oldValues,
									log.newValues,
								);

								return (
									<TableRow key={log.id}>
										<TableCell className="text-xs text-muted-foreground">
											{new Date(log.createdAt).toLocaleString(
												"tr-TR",
											)}
										</TableCell>
										<TableCell className="text-sm">
											{log.userEmail ?? "—"}
										</TableCell>
										<TableCell>
											<Badge variant="outline">
												{log.entityType}
											</Badge>{" "}
											<span className="font-mono text-xs text-muted-foreground">
												#{log.entityId}
											</span>
										</TableCell>
										<TableCell>
											<Badge
												variant={
													AUDIT_ACTION_TONES[log.action] ??
													"secondary"
												}
											>
												{AUDIT_ACTION_LABELS[log.action] ??
													log.action}
											</Badge>
										</TableCell>
										<TableCell>
											{fields.length > 0 ? (
												<div className="flex items-center gap-2">
													<Badge
														variant="secondary"
														className="tabular-nums"
													>
														{fields.length} alan
													</Badge>
													<span className="truncate text-xs text-muted-foreground">
														{fields.slice(0, 2).join(", ")}
													</span>
												</div>
											) : (
												<span className="text-xs text-muted-foreground">
													—
												</span>
											)}
										</TableCell>
										<TableCell className="text-right">
											<RowActions>
												{fields.length > 0 ? (
													<RowAction
														icon={EyeIcon}
														label="Detay"
														onClick={() => setDetail(log)}
													/>
												) : null}
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
				<DialogContent className="sm:max-w-lg">
					<DialogHeader>
						<DialogTitle>
							{detail
								? `${detail.entityType} #${detail.entityId} — ${AUDIT_ACTION_LABELS[detail.action] ?? detail.action}`
								: ""}
						</DialogTitle>
					</DialogHeader>
					{detailValues ? (
						<div className="grid grid-cols-[auto_1fr_1fr] gap-x-4 gap-y-2 text-xs">
							<span className="font-semibold text-muted-foreground">
								Alan
							</span>
							<span className="font-semibold text-muted-foreground">
								Eski
							</span>
							<span className="font-semibold text-muted-foreground">
								Yeni
							</span>
							{detailValues.fields.map((field) => {
								const oldValue = detailValues.oldParsed?.[field];
								const newValue = detailValues.newParsed?.[field];
								const changed =
									JSON.stringify(oldValue) !== JSON.stringify(newValue);
								return (
									<Fragment key={field}>
										<span className="font-mono">{field}</span>
										<span
											className={
												changed
													? "rounded bg-warning-soft px-1.5 py-0.5"
													: undefined
											}
										>
											{formatFieldValue(oldValue)}
										</span>
										<span
											className={
												changed
													? "rounded bg-success-soft px-1.5 py-0.5"
													: undefined
											}
										>
											{formatFieldValue(newValue)}
										</span>
									</Fragment>
								);
							})}
						</div>
					) : null}
				</DialogContent>
			</Dialog>
		</div>
	);
}
