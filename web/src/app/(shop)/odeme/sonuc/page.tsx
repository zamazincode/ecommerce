import { PaymentStatusChecker } from "@/components/checkout/payment-status-checker";
import Link from "next/link";

type SearchParams = Promise<{ durum?: string; siparis?: string }>;

const MESSAGES: Record<string, { title: string; description: string }> = {
	basarili: {
		title: "Ödemeniz alındı",
		description: "Siparişiniz onaylandı.",
	},
	basarisiz: {
		title: "Ödeme başarısız",
		description: "Kartınızdan çekim yapılamadı. Lütfen tekrar deneyin.",
	},
	inceleniyor: {
		title: "Ödemeniz inceleniyor",
		description:
			"Tutar uyuşmazlığı tespit edildi, ekibimiz kısa süre içinde sizinle iletişime geçecek.",
	},
	gecersiz: {
		title: "Geçersiz istek",
		description: "Bu bağlantı geçerli değil.",
	},
	hata: {
		title: "Beklenmeyen bir hata oluştu",
		description: "Lütfen tekrar deneyin.",
	},
};

export default async function PaymentResultPage({
	searchParams,
}: {
	searchParams: SearchParams;
}) {
	const { durum, siparis } = await searchParams;
	const message = MESSAGES[durum ?? "hata"] ?? MESSAGES.hata;

	return (
		<main className="container-x py-16 text-center">
			<h1 className="text-xl font-semibold">{message.title}</h1>
			<p className="mt-2 text-muted-foreground">{message.description}</p>
			{siparis ? (
				<p className="mt-4 text-sm">
					Sipariş No: <strong>{siparis}</strong>
				</p>
			) : null}

			{durum === "inceleniyor" && siparis ? (
				<PaymentStatusChecker orderNumber={siparis} />
			) : null}

			<Link href="/" className="mt-6 inline-block underline">
				Ana sayfaya dön
			</Link>
		</main>
	);
}
