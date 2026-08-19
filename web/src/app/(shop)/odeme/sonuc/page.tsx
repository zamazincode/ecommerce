import Link from "next/link";
import { CheckIcon, ClockIcon, XIcon } from "lucide-react";
import { PaymentStatusChecker } from "@/components/checkout/payment-status-checker";
import { Button } from "@/components/ui/button";
import { cn } from "@/lib/utils";

type SearchParams = Promise<{ durum?: string; siparis?: string }>;

type ResultTone = "success" | "destructive" | "warning" | "muted";

const TONE_CLASSES: Record<ResultTone, string> = {
	success: "bg-success-soft text-success",
	destructive: "bg-destructive/10 text-destructive",
	warning: "bg-warning-soft text-warning",
	muted: "bg-muted text-muted-foreground",
};

const MESSAGES: Record<
	string,
	{
		title: string;
		description: string;
		tone: ResultTone;
		icon: typeof CheckIcon;
	}
> = {
	basarili: {
		title: "Ödemeniz alındı",
		description: "Siparişiniz onaylandı.",
		tone: "success",
		icon: CheckIcon,
	},
	basarisiz: {
		title: "Ödeme başarısız",
		description: "Kartınızdan çekim yapılamadı. Lütfen tekrar deneyin.",
		tone: "destructive",
		icon: XIcon,
	},
	inceleniyor: {
		title: "Ödemeniz inceleniyor",
		description:
			"Tutar uyuşmazlığı tespit edildi, ekibimiz kısa süre içinde sizinle iletişime geçecek.",
		tone: "warning",
		icon: ClockIcon,
	},
	gecersiz: {
		title: "Geçersiz istek",
		description: "Bu bağlantı geçerli değil.",
		tone: "muted",
		icon: XIcon,
	},
	hata: {
		title: "Beklenmeyen bir hata oluştu",
		description: "Lütfen tekrar deneyin.",
		tone: "muted",
		icon: XIcon,
	},
};

export default async function PaymentResultPage({
	searchParams,
}: {
	searchParams: SearchParams;
}) {
	const { durum, siparis } = await searchParams;
	const message = MESSAGES[durum ?? "hata"] ?? MESSAGES.hata;
	const Icon = message.icon;

	return (
		<main className="container-x flex flex-col items-center py-16 text-center">
			<div
				className={cn(
					"grid size-16 place-items-center rounded-full",
					TONE_CLASSES[message.tone],
				)}
			>
				<Icon className="size-7" />
			</div>
			<h1 className="mt-4 font-heading text-2xl font-semibold">
				{message.title}
			</h1>
			<p className="mt-2 max-w-md text-muted-foreground">
				{message.description}
			</p>
			{siparis ? (
				<p className="mt-4 rounded-lg bg-muted px-3 py-1.5 font-mono text-sm">
					{siparis}
				</p>
			) : null}

			{durum === "inceleniyor" && siparis ? (
				<PaymentStatusChecker orderNumber={siparis} />
			) : null}

			<div className="mt-6 flex items-center gap-2">
				<Button
					render={<Link href="/hesabim/siparislerim" />}
					nativeButton={false}
				>
					Siparişlerim
				</Button>
				<Button
					variant="outline"
					render={<Link href="/" />}
					nativeButton={false}
				>
					Ana Sayfa
				</Button>
			</div>
		</main>
	);
}
