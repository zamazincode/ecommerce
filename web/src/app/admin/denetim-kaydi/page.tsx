"use client";

import { useState } from "react";
import { useAdminAuditLogs } from "@/hooks/use-admin-audit-logs";
import {
	Table,
	TableBody,
	TableCell,
	TableHead,
	TableHeader,
	TableRow,
} from "@/components/ui/table";
import { Skeleton } from "@/components/ui/skeleton";

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

export default function AdminAuditLogPage() {
	const [entityType, setEntityType] = useState("");
	const [action, setAction] = useState("");
	const { data, isLoading } = useAdminAuditLogs({ entityType, action });

	return (
		<div>
			<h1 className="mb-6 text-xl font-semibold">Denetim Kaydı</h1>

			<div className="mb-4 flex gap-2">
				<select
					value={entityType}
					onChange={(e) => setEntityType(e.target.value)}
					className="rounded-md border px-3 py-2 text-sm"
				>
					<option value="">Tüm varlıklar</option>
					{ENTITY_TYPES.map((t) => (
						<option key={t} value={t}>
							{t}
						</option>
					))}
				</select>
				<select
					value={action}
					onChange={(e) => setAction(e.target.value)}
					className="rounded-md border px-3 py-2 text-sm"
				>
					<option value="">Tüm işlemler</option>
					{ACTIONS.map((a) => (
						<option key={a} value={a}>
							{a}
						</option>
					))}
				</select>
			</div>

			{isLoading ? (
				<Skeleton className="h-64 w-full" />
			) : (
				<Table>
					<TableHeader>
						<TableRow>
							<TableHead>Tarih</TableHead>
							<TableHead>Kullanıcı</TableHead>
							<TableHead>Varlık</TableHead>
							<TableHead>İşlem</TableHead>
							<TableHead>Değişiklik</TableHead>
						</TableRow>
					</TableHeader>
					<TableBody>
						{data?.items.map((log) => (
							<TableRow key={log.id}>
								<TableCell className="text-xs text-muted-foreground">
									{new Date(log.createdAt).toLocaleString("tr-TR")}
								</TableCell>
								<TableCell className="text-sm">
									{log.userEmail ?? "—"}
								</TableCell>
								<TableCell>
									{log.entityType} #{log.entityId}
								</TableCell>
								<TableCell>{log.action}</TableCell>
								<TableCell>
									<AuditDiff
										oldValues={log.oldValues}
										newValues={log.newValues}
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

function AuditDiff({
	oldValues,
	newValues,
}: {
	oldValues: string | null;
	newValues: string | null;
}) {
	// OldValues/NewValues STRING olarak geliyor (jsonb kolonu) — istemci
	// JSON.parse eder, object'e çevirmek gereksiz iki kez ayrıştırma olurdu.
	let parsed: Record<string, unknown> | null = null;
	try {
		parsed = newValues
			? JSON.parse(newValues)
			: oldValues
				? JSON.parse(oldValues)
				: null;
	} catch {
		parsed = null;
	}

	if (!parsed) return <span className="text-xs text-muted-foreground">—</span>;

	return (
		<pre className="max-w-xs overflow-x-auto rounded bg-muted p-2 text-xs">
			{JSON.stringify(parsed, null, 2)}
		</pre>
	);
}
