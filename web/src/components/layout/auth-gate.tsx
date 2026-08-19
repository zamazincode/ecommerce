"use client";

import { useEffect } from "react";
import { useRouter } from "next/navigation";
import { LoaderIcon } from "lucide-react";
import useSession from "@/hooks/use-session";

// (auth)/layout.tsx'ten ayrıştırıldı: Header artık sunucu bileşeni olarak
// o layout'un içinde render ediliyor, bu yüzden yönlendirme mantığı client
// alt bileşene taşındı (bir "use client" dosyası async sunucu bileşenini
// doğrudan render edemiyor).
export function AuthGate({ children }: { children: React.ReactNode }) {
	const { data: user, isPending } = useSession();
	const router = useRouter();

	useEffect(() => {
		if (!isPending && user) router.replace("/hesabim");
	}, [isPending, user, router]);

	// Oturum sorgusu dönene kadar beyaz ekran yerine ortalanmış gösterge —
	// ortalama kabuğu (min-h + grid place-items-center) layout.tsx sağlıyor.
	if (isPending) {
		return (
			<LoaderIcon className="size-8 animate-spin text-muted-foreground" />
		);
	}

	// Giriş yapmış kullanıcı /hesabim'e yönlendiriliyor (yukarıdaki effect) —
	// yönlendirme tamamlanana kadar burada bir şey göstermiyoruz.
	if (user) return null;

	return <>{children}</>;
}
