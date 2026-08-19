import type { LucideIcon } from "lucide-react";

import { cn } from "@/lib/utils";
import { Card } from "@/components/ui/card";

type StatCardTone = "default" | "success" | "warning" | "danger";

interface StatCardProps {
	label: string;
	value: React.ReactNode;
	icon: LucideIcon;
	hint?: React.ReactNode;
	tone?: StatCardTone;
	className?: string;
}

const TONE_CLASSES: Record<StatCardTone, string> = {
	default: "bg-primary-soft text-primary",
	success: "bg-success-soft text-success",
	warning: "bg-warning-soft text-warning",
	danger: "bg-destructive/10 text-destructive",
};

/** Dashboard KPI kartı — Faz D, sorun #24: ikon + trend olmayan çıplak kartların yerini alıyor. */
export function StatCard({
	label,
	value,
	icon: Icon,
	hint,
	tone = "default",
	className,
}: StatCardProps) {
	return (
		<Card size="sm" className={className + " px-4"}>
			<div className="flex items-start justify-between">
				<p className="text-sm text-muted-foreground">{label}</p>
				<div
					className={cn(
						"grid size-10 shrink-0 place-items-center rounded-xl",
						TONE_CLASSES[tone],
					)}
				>
					<Icon className="size-5" />
				</div>
			</div>
			<p className="font-heading text-2xl font-semibold tabular-nums">
				{value}
			</p>
			{hint ? (
				<p className="text-xs text-muted-foreground">{hint}</p>
			) : null}
		</Card>
	);
}
