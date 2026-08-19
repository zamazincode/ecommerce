import { CheckIcon } from "lucide-react";
import { cn } from "@/lib/utils";
import { ORDER_STATUS_LABELS } from "@/lib/enums";
import type { components } from "@/types/api";

type OrderStatus = components["schemas"]["OrderStatus"];

// Normal akış — İptal(5) ve İade(6) burada yok, çağıran taraf onları tek bir
// `destructive` rozetle ayrı gösteriyor (bkz. sipariş detay sayfası).
const FLOW: OrderStatus[] = [0, 1, 2, 3, 4];

/** `CheckoutSteps`in sipariş durumu sürümü — Beklemede → Ödendi → Hazırlanıyor → Kargoda → Teslim. */
export function OrderStatusTimeline({ status }: { status: OrderStatus }) {
	const currentIndex = FLOW.indexOf(status);

	return (
		<ol className="flex items-start">
			{FLOW.map((step, index) => {
				const isCompleted = currentIndex >= 0 && index < currentIndex;
				const isActive = index === currentIndex;

				return (
					<li
						key={step}
						className="flex flex-1 items-center last:flex-none"
					>
						<div className="flex flex-col items-center gap-1.5">
							<span
								className={cn(
									"grid size-8 shrink-0 place-items-center rounded-full text-xs font-semibold transition-colors",
									isCompleted &&
										"bg-primary text-primary-foreground",
									isActive &&
										"bg-primary text-primary-foreground ring-4 ring-primary/20",
									!isCompleted &&
										!isActive &&
										"bg-muted text-muted-foreground",
								)}
							>
								{isCompleted ? (
									<CheckIcon className="size-3.5" />
								) : (
									index + 1
								)}
							</span>
							<span
								className={cn(
									"max-w-16 text-center text-[11px] font-medium",
									isCompleted || isActive
										? "text-foreground"
										: "text-muted-foreground",
								)}
							>
								{ORDER_STATUS_LABELS[step]}
							</span>
						</div>
						{index < FLOW.length - 1 ? (
							<div
								className={cn(
									"mx-1 h-px flex-1 transition-colors",
									isCompleted ? "bg-primary" : "bg-border",
								)}
							/>
						) : null}
					</li>
				);
			})}
		</ol>
	);
}
