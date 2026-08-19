import Link from "next/link";
import { TruckIcon, ClockIcon } from "lucide-react";
import { FREE_SHIPPING_THRESHOLD } from "@/lib/shipping";

/** dr.com.tr'deki en üst ince duyuru çubuğu. Statik içerik, veri gerekmez. */
export function AnnouncementBar() {
	return (
		<div className="h-9 bg-primary text-xs text-primary-foreground">
			<div className="container-x flex h-full items-center justify-between gap-4">
				<div className="flex items-center gap-5">
					<span className="flex items-center gap-1.5">
						<TruckIcon className="size-3.5" />
						{FREE_SHIPPING_THRESHOLD}₺ üzeri kargo bedava
					</span>
					<span className="hidden items-center gap-1.5 sm:flex">
						<ClockIcon className="size-3.5" />
						16:00&apos;a kadar aynı gün kargo
					</span>
				</div>
				<div className="hidden gap-5 md:flex">
					<Link
						href="/hesabim/siparislerim"
						className="hover:underline hover:underline-offset-4"
					>
						Sipariş Takibi
					</Link>
					<Link
						href="/hesabim/favorilerim"
						className="hover:underline hover:underline-offset-4"
					>
						Favorilerim
					</Link>
					<Link
						href="/kategori/kitap"
						className="hover:underline hover:underline-offset-4"
					>
						Kampanyalar
					</Link>
				</div>
			</div>
		</div>
	);
}
