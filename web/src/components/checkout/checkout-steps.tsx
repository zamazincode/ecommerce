import { CheckIcon } from "lucide-react";
import { cn } from "@/lib/utils";

const STEPS = [
	{ key: "address", label: "Adres" },
	{ key: "payment", label: "Ödeme" },
	{ key: "confirm", label: "Onay" },
] as const;

interface CheckoutStepsProps {
	current: "address" | "payment";
}

/**
 * Ödeme akışının 3 adımı. "Onay" adımı `/odeme/sonuc`'ta — bu bileşen orada
 * render edilmiyor, o yüzden burada hep "gelecek" görünür.
 */
export function CheckoutSteps({ current }: CheckoutStepsProps) {
	const currentIndex = STEPS.findIndex((step) => step.key === current);

	return (
		<ol className="mb-8 flex items-start">
			{STEPS.map((step, index) => {
				const isCompleted = index < currentIndex;
				const isActive = index === currentIndex;

				return (
					<li
						key={step.key}
						className="flex flex-1 items-center last:flex-none"
					>
						<div className="flex flex-col items-center gap-1.5">
							<span
								className={cn(
									"grid size-9 shrink-0 place-items-center rounded-full text-sm font-semibold transition-colors",
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
									<CheckIcon className="size-4" />
								) : (
									index + 1
								)}
							</span>
							<span
								className={cn(
									"text-xs font-medium whitespace-nowrap",
									isCompleted || isActive
										? "text-foreground"
										: "text-muted-foreground",
								)}
							>
								{step.label}
							</span>
						</div>
						{index < STEPS.length - 1 ? (
							<div
								className={cn(
									"mx-2 h-px flex-1 transition-colors",
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
