import {
	ShieldCheckIcon,
	RotateCcwIcon,
	CreditCardIcon,
	HeadsetIcon,
	type LucideIcon,
} from "lucide-react";

const ITEMS: { icon: LucideIcon; title: string; description: string }[] = [
	{
		icon: ShieldCheckIcon,
		title: "256-bit SSL",
		description: "Güvenli ödeme altyapısı",
	},
	{
		icon: RotateCcwIcon,
		title: "14 Gün İçinde İade",
		description: "Koşulsuz iade garantisi",
	},
	{
		icon: CreditCardIcon,
		title: "Taksitli Ödeme",
		description: "Tüm kredi kartlarına taksit imkânı",
	},
	{
		icon: HeadsetIcon,
		title: "7/24 Destek",
		description: "Her zaman yanınızdayız",
	},
];

/** Footer'ın üstündeki güven rozetleri şeridi — dr.com.tr'deki tanıdık desen. */
export function TrustBadges() {
	return (
		<div className="container-x grid grid-cols-2 gap-4 py-8 lg:grid-cols-4">
			{ITEMS.map((item) => (
				<div key={item.title} className="flex items-center gap-3">
					<div className="grid size-10 shrink-0 place-items-center rounded-full bg-primary-soft text-primary">
						<item.icon className="size-5" />
					</div>
					<div>
						<p className="text-sm font-medium">{item.title}</p>
						<p className="text-xs text-muted-foreground">
							{item.description}
						</p>
					</div>
				</div>
			))}
		</div>
	);
}
