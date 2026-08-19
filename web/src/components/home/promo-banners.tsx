import { TruckIcon, ClockIcon, SmartphoneIcon, type LucideIcon } from "lucide-react";
import { FREE_SHIPPING_THRESHOLD } from "@/lib/shipping";
import { cn } from "@/lib/utils";

const ITEMS: {
	icon: LucideIcon;
	title: string;
	description: string;
	wrapClassName: string;
}[] = [
	{
		icon: TruckIcon,
		title: "Kargo Bedava",
		description: `${FREE_SHIPPING_THRESHOLD} ₺ üzeri siparişlerde`,
		wrapClassName: "bg-primary-soft text-primary",
	},
	{
		icon: ClockIcon,
		title: "Aynı Gün Kargo",
		description: "16:00'a kadar verilen siparişlerde",
		wrapClassName: "bg-red-soft text-red",
	},
	{
		icon: SmartphoneIcon,
		title: "Mobil Uygulama",
		description: "Yakında",
		wrapClassName: "bg-warning-soft text-warning",
	},
];

/** Anasayfa hero'nun altındaki üç promosyon kartı. */
export function PromoBanners() {
	return (
		<div className="grid gap-4 md:grid-cols-3">
			{ITEMS.map((item) => (
				<div
					key={item.title}
					className={cn(
						"flex items-center gap-4 rounded-2xl p-6",
						item.wrapClassName,
					)}
				>
					<div className="grid size-12 shrink-0 place-items-center rounded-xl bg-background/70">
						<item.icon className="size-6" />
					</div>
					<div>
						<p className="font-medium">{item.title}</p>
						<p className="text-sm opacity-80">{item.description}</p>
					</div>
				</div>
			))}
		</div>
	);
}
